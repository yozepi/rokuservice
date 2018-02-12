using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RokuService.Models;
using RokuService.Services;

namespace RokuService.Controllers
{
    [Route("")]
    public class AdminController : Controller
    {
        readonly RokuManager Manager;

        public AdminController(RokuManager manager)
        {
            this.Manager = manager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rokus = await Manager.ListRokusAsync(false);
            return View(rokus);
        }

        [HttpGet]
        [Route("add")]
        public IActionResult AddRoku()
        {
            return View();
        }

        [HttpPost]
        [Route("add")]
        public async Task<IActionResult> AddRoku([FromForm] AddRokuModel model)
        {
            IPAddress rokuIp;

            if (string.IsNullOrWhiteSpace(model.IPAddress))
            {
                this.ModelState.AddModelError("IPAddress", "I.P. Address is required.");
                return View(model);
            }
            if (!IPAddress.TryParse(model.IPAddress, out rokuIp))
            {
                this.ModelState.AddModelError("IPAddress", "I.P. Address is not a valid address.");
                return View(model);
            }

            string name = string.IsNullOrWhiteSpace(model.Name?.Trim()) ? null : model.Name.Trim();
            var roku = await Manager.AddRokuAsync(rokuIp, name);
            if(roku == null)
            {
                this.ModelState.AddModelError("IPAddress", "A roku with this I.P. address could not be found on your local network.");
                return View(model);
            }

            return RedirectToAction("Index");
        }

    }

}