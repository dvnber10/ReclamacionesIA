using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace WebUI.Views.Cuenta
{
    public class AccesoDenegado : PageModel
    {
        private readonly ILogger<AccesoDenegado> _logger;

        public AccesoDenegado(ILogger<AccesoDenegado> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}