using System.Security.Claims;
using Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebUI.Controllers
{
    public sealed class CuentaController : Controller
    {
        private readonly ReclamacionesDbContext _db;
        private readonly PasswordHasher<Business.Entities.Analyst> _hasher = new();

        public CuentaController(ReclamacionesDbContext db) => _db = db;

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string email, string password, string? returnUrl = null,
            CancellationToken ct = default)
        {
            var analista = await _db.Analistas
                .FirstOrDefaultAsync(a => a.Email == email, ct);

            var valido = analista is not null &&
                _hasher.VerifyHashedPassword(analista, analista.PasswordHash, password)
                    != PasswordVerificationResult.Failed;

            if (!valido)
            {
                ModelState.AddModelError("", "Correo o contraseña incorrectos.");
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            var claims = new List<Claim>
            {
                new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, analista!.Id.ToString()),
                new System.Security.Claims.Claim(ClaimTypes.Name, analista.Nombre),
                new System.Security.Claims.Claim(ClaimTypes.Email, analista.Email),
                new System.Security.Claims.Claim(ClaimTypes.Role, analista.Rol)
            };

            var identidad = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidad));

            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Panel" : returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccesoDenegado() => View();
    }
}