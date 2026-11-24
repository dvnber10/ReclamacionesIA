using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace WebUI.Views.Seguimiento
{
    public class Confirmacion : PageModel
    {
        private readonly ILogger<Confirmacion> _logger;

        public Confirmacion(ILogger<Confirmacion> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}