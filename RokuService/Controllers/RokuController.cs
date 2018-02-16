using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RokuService.Services;
using yozepi.Roku;

namespace RokuService.Controllers
{
    [Route("api/rokus")]
    public class RokuController : Controller
    {
        int pause = 150;

        readonly RokuManager Manager;
        readonly ILogger Logger;

        public RokuController(RokuManager manager, ILogger<RokuController> logger)
        {
            this.Manager = manager;
            this.Logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ListRokus([FromQuery] bool forceDiscovery)
        {
            using (Logger.BeginScope("api->ListRokus"))
            {
                try
                {
                    var rokus = await Manager.ListRokusAsync(forceDiscovery);

                    //Organize the list first by Alpha Name, then by ids
                    var rokuList = new List<object>();

                    rokuList.AddRange(rokus
                        .Where(r => !string.IsNullOrEmpty(r.Name))
                        .OrderBy(r => r.Name)
                        .Select(r => new { Id = r.Id, name = r.Name }));

                    rokuList.AddRange(rokus
                        .Where(r => string.IsNullOrEmpty(r.Name))
                        .OrderBy(r => r.Id)
                        .Select(r => new { Id = r.Id }));

                    return Ok(rokuList);
                }
                catch (Exception ex)
                {
                    return LogAndReturnException(ex);
                }
            }
        }

        [HttpGet]
        [Route("{rokuId}/info")]
        public async Task<IActionResult> GetInfo([FromRoute] string rokuId)
        {
            using (Logger.BeginScope("api->GetInfo"))
            {
                try
                {
                    var roku = await Manager.GetRokuAsync(rokuId);
                    if (roku == null)
                        return RokuNotFoundResult(rokuId);

                    return Ok(roku.Info);
                }
                catch (Exception ex)
                {
                    return LogAndReturnException(ex);
                }
            }
        }

        [HttpGet]
        [Route("{rokuId}/apps")]
        public async Task<IActionResult> ListApps([FromRoute] string rokuId, [FromQuery] string filter)
        {
            using (Logger.BeginScope("api->ListApps"))
            {
                try
                {
                    var roku = await Manager.GetRokuAsync(rokuId);
                    if (roku == null)
                        return RokuNotFoundResult(rokuId);

                    if (string.IsNullOrWhiteSpace(filter))
                        return Json(roku.Apps.OrderBy(a => a.Text));

                    return Json(FilteredAppList(roku, filter));
                }
                catch (Exception ex)
                {
                    return LogAndReturnException(ex);
                }
            }
        }

        [HttpGet]
        [Route("{rokuId}/apps/launch")]
        public async Task<IActionResult> LaunchApp([FromRoute] string rokuId, [FromQuery] string filter)
        {
            using (Logger.BeginScope("api->LaunchApp"))
            {
                return await LaunchApp(rokuId, (roku) => FilteredAppList(roku, filter));
            }
        }

        [HttpGet]
        [Route("{rokuId}/apps/launch/{id}")]
        public async Task<IActionResult> LaunchApp([FromRoute] string rokuId, [FromRoute] int id)
        {
            using (Logger.BeginScope("api->LaunchApp"))
            {
                return await LaunchApp(rokuId, (roku) => roku.Apps.Where(a => a.Id == id));
            }
        }

        [HttpGet]
        [Route("{rokuId}/apps/active")]
        public async Task<IActionResult> GetActiveApp([FromRoute] string rokuId)
        {
            using (Logger.BeginScope("api->GetActiveApp"))
            {
                try
                {
                    var roku = await Manager.GetRokuAsync(rokuId);
                    if (roku == null)
                        return RokuNotFoundResult(rokuId);

                    var rokuResult = await roku.GetActiveAppAsync();
                    if (rokuResult.IsSuccess)
                        return Json(rokuResult);

                    return RokuErrorResponse(rokuResult);
                }
                catch (Exception ex)
                {
                    return LogAndReturnException(ex);
                }
            }
        }

        [HttpGet]
        [Route("{rokuId}/send/{command}/{count?}")]
        public async Task<IActionResult> Send([FromRoute] string rokuId, [FromRoute]CommandKeys command, [FromRoute]int count = 1)
        {
            using (Logger.BeginScope("api->Send"))
            {
                try
                {
                    return await Send(
                        rokuId,
                        () => Logger.LogInformation("Sending command {cmd} {count} time(s).", command, count),
                        (r) => r.KeypressAsync(command),
                        count);
                }
                catch (Exception ex)
                {
                    return LogAndReturnException(ex);
                }
            }
        }

        [HttpGet]
        [Route("{rokuId}/send/{ch:regex(^.$)}/{count?}")]
        public async Task<IActionResult> Send([FromRoute] string rokuId, [FromRoute]char ch, [FromRoute]int count = 1)
        {
            using (Logger.BeginScope("api->Send"))
            {
                return await Send(
                    rokuId,
                    () => Logger.LogInformation("Sending character {ch} {count} time(s).", ch, count),
                    (r) => r.KeypressAsync(ch),
                    count);
            }
        }

        [HttpGet]
        [Route("{rokuId}/send/space/{count?}")]
        public async Task<IActionResult> SendSpace([FromRoute] string rokuId, [FromRoute]int count = 1)
        {
            using (Logger.BeginScope("api->SendSpace"))
            {
                return await Send(
                    rokuId,
                    () => Logger.LogInformation("Sending character ' ' {count} time(s) to .", count),
                    (r) => r.KeypressAsync(' '),
                    count);
            }
        }

        [HttpGet]
        [Route("{rokuId}/type")]
        public async Task<IActionResult> SendText([FromRoute] string rokuId, [FromQuery] string text)
        {
            using (Logger.BeginScope("api->SentText"))
            {
                var roku = await Manager.GetRokuAsync(rokuId);
                if (roku == null)
                    return RokuNotFoundResult(rokuId);

                if (string.IsNullOrEmpty(text))
                {
                    return ErrorResponse("text is required.", HttpStatusCode.BadRequest);
                }

                Logger.LogInformation("Sending Text: \"{text}\".", text);
                Logger.LogInformation("Character Delay: {pause} milliseconds.", pause);
                Logger.LogInformation("Roku Endpoint: {ep}", roku.Url);

                foreach (var ch in text)
                {
                    var rokuResult = await roku.KeypressAsync(ch);
                    if (!rokuResult.IsSuccess)
                        return RokuErrorResponse(rokuResult);

                    await Task.Delay(pause);
                }
                return Ok();
            }
        }


        #region support methods:

        private async Task<IActionResult> LaunchApp(string rokuId, Func<IRokuRemote, IEnumerable<RokuApp>> appFilter)
        {
            try
            {
                var roku = await Manager.GetRokuAsync(rokuId);
                if (roku == null)
                    return RokuNotFoundResult(rokuId);

                var appList = appFilter(roku).ToArray();
                if (appList.Length == 0)
                {
                    Logger.LogWarning("App not found");
                    return NotFound(new { message = "no matching apps found", code = "NoMatch" });
                }

                if (appList.Length > 1)
                {
                    Logger.LogWarning("Multiple matches: {matches}", appList.Select(a => new { a.Id, name = a.Text }));
                    return BadRequest(new { message = "More than one app matches.", code = "AmbiguousMatch", apps = appList });
                }

                var app = appList[0];
                Logger.LogInformation("Launching App: {app}", new { app.Id, name = app.Text });
                Logger.LogInformation("Roku Endpoint: {ep}", roku.Url);
                var rokuResult = await roku.LaunchAppAsync(app.Id);
                if (rokuResult.IsSuccess)
                {
                    Logger.LogInformation("App launched.");
                    return Ok(new { message = "app launched.", code = "AppLaunched", appName = app.Text, appId = app.Id });
                }
                if (rokuResult.StatusCode == HttpStatusCode.NoContent)
                {
                    Logger.LogWarning("App is already running.");
                    return BadRequest(new { message = "App is already running.", code = "AppRunning" });
                }
                return RokuErrorResponse(rokuResult);

            }
            catch (Exception ex)
            {
                return LogAndReturnException(ex);
            }
        }

        private IEnumerable<RokuApp> FilteredAppList(IRokuRemote roku, string filter)
        {

            filter = filter.ToLowerInvariant();
            var result = roku.Apps
                .Where(a => a.Text.ToLowerInvariant().Contains(filter))
                .OrderBy(a => a.Text);

            Logger.LogInformation("{count} app(s) matched filter: {filter}", result.Count(), filter);

            return result;
        }

        private async Task<IActionResult> Send(string rokuId, Action Log, Func<IRokuRemote, Task<ICommandResponse>> sendCommand, int count)
        {
            try
            {
                if (count < 1)
                {
                    return ErrorResponse("count must be 1 or greater.", HttpStatusCode.BadRequest);
                }

                var roku = await Manager.GetRokuAsync(rokuId);
                if (roku == null)
                    return RokuNotFoundResult(rokuId);

                Log();
                Logger.LogInformation("Character Delay: {pause} milliseconds.", pause);
                Logger.LogInformation("Roku Endpoint: {ep}", roku.Url);

                for (int c = 0; c < count; c++)
                {
                    var rokuResult = await sendCommand(roku);
                    if (!rokuResult.IsSuccess)
                        return RokuErrorResponse(rokuResult);

                    await Task.Delay(pause);
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return LogAndReturnException(ex);
            }
        }


        private IActionResult RokuErrorResponse(ICommandResponse rokuResult)
        {
            return ErrorResponse(rokuResult.StatusDescription, rokuResult.StatusCode);
        }

        private IActionResult ErrorResponse(string message, HttpStatusCode statusCode)
        {
            Logger.LogWarning("Error! StatusCode: {statusCode}, Message: {message}", statusCode, message);
            var errorResponse = Json(new { message = message });
            errorResponse.StatusCode = (int)statusCode;
            return errorResponse;
        }

        private IActionResult ErrorResponse(string message, string code, HttpStatusCode statusCode)
        {
            Logger.LogWarning("Error!Code: {code}, StatusCode: {statusCode}, Message: {message}", code, statusCode, message);
            var errorResponse = Json(new { message = message, code = code });
            errorResponse.StatusCode = (int)statusCode;
            return errorResponse;
        }

        private IActionResult RokuNotFoundResult(string rokuId)
        {
            Logger.LogWarning("Roku not found! Roku ID: {rokuId}", rokuId);
            var response = NotFound(new { message = "Roku not found.", code = "RokuNotFound", id = rokuId });
            return response;
        }

        private IActionResult LogAndReturnException(Exception ex)
        {
            Logger.LogError(ex, "");
            return ErrorResponse("Server Error", HttpStatusCode.InternalServerError);
        }

        #endregion //support methods:
    }
}
