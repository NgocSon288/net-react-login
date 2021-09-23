using Google.Models;
using Google.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Google.Controllers
{
    [Route("Login")]
    public class LoginController : Controller
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;

        public LoginController(SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet("google-login")]
        public async Task<IActionResult> Index()
        {

            await _signInManager.SignOutAsync();

            var externalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            var model = new LoginModel();
            model.AuthenticationSchemes = externalLogins;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // redirectUrl - là Url sẽ chuyển hướng đến - sau khi CallbackPath (/dang-nhap-tu-google) thi hành xong
            // nó bằng identity/account/externallogin?handler=Callback 
            // tức là gọi OnGetCallbackAsync 
            //var redirectUrl = Url.Page("./Login", pageHandler: "Callback", values: new { returnUrl });
            var redirectUrl = Url.Action(nameof(Callback), "Login", new { returnUrl });

            // Cấu hình 
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

            // Chuyển hướng đến dịch vụ ngoài (Googe, Facebook)
            return new ChallengeResult(provider, properties);
        }

        [HttpGet("Callback")]
        public async Task<IActionResult> Callback(string returnUrl = null, string remoteError = null)
        {
            var ErrorMessage = "";

            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Lỗi provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Lấy thông tin do dịch vụ ngoài chuyển đến
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Lỗi thông tin từ dịch vụ đăng nhập.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Đăng nhập bằng thông tin LoginProvider, ProviderKey từ info cung cấp bởi dịch vụ ngoài
            // User nào có 2 thông tin này sẽ được đăng nhập - thông tin này lưu tại bảng UserLogins của Database
            // Trường LoginProvider và ProviderKey ---> tương ứng UserId 
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                // User đăng nhập thành công vào hệ thống theo thông tin info
                // Đã có account và đã liên kết với dịch vụ ngoài

                return RedirectToAction(nameof(Success), "Login");
            }
            if (result.IsLockedOut)
            {
                // Bị tạm khóa
                // Có tài khoản nhưng bị khóa
                return RedirectToAction(nameof(Lockout), "Login");
            }
            else
            {
                // Có tài khoản nhưng chưa liên kết với google,
                // Chưa có tài khoản


                var userExisted = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (userExisted != null)
                {
                    // Đã có Acount, đã liên kết với tài khoản ngoài - nhưng không đăng nhập được
                    // có thể do chưa kích hoạt email
                    return RedirectToAction(nameof(Exists), "Login");

                }

                // Chưa có Account liên kết với tài khoản ngoài
                // Hiện thị form để thực hiện bước tiếp theo ở OnPostConfirmationAsync
                var ReturnUrl = returnUrl;
                var ProviderDisplayName = info.ProviderDisplayName;
                if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    // Có thông tin về email từ info, lấy email này hiện thị ở Form

                    var email = info.Principal.FindFirstValue(ClaimTypes.Email);

                    var user = await _userManager.FindByEmailAsync(email);

                    if (user != null)
                    {

                        // xác nhận email luôn nếu chưa xác nhận
                        if (!user.EmailConfirmed)
                        {
                            var codeactive = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                            await _userManager.ConfirmEmailAsync(user, codeactive);
                        }

                        // Thực hiện liên kết info và user
                        var resultAdd = await _userManager.AddLoginAsync(user, info);
                        if (resultAdd.Succeeded)
                        {
                            // Thực hiện login    
                            await _signInManager.SignInAsync(user, isPersistent: false);
                        }

                        return RedirectToAction(nameof(Exists), "Login");
                    }
                    else
                    {
                        user = new AppUser()
                        {
                            UserName = email,
                            Email = email
                        };

                        var resultNew = await _userManager.CreateAsync(user);

                        if (resultNew.Succeeded)
                        {
                            // Liên kết tài khoản ngoài với tài khoản vừa tạo
                            resultNew = await _userManager.AddLoginAsync(user, info);

                            if (resultNew.Succeeded)
                            { 
                                // Email tạo tài khoản và email từ info giống nhau -> xác thực email luôn
                                 
                                var codeactive = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                                await _userManager.ConfirmEmailAsync(user, codeactive);
                                await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);  
                                 
                                // Đăng nhập ngay do không yêu cầu xác nhận email
                                await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);

                                return RedirectToAction(nameof(NoExists), "Login");
                            }
                        }
                    }
                }



                return RedirectToAction(nameof(Exists), "Login");
            }
        }

        [HttpGet("Success")]
        public IActionResult Success()
        {
            return View();
        }

        [HttpGet("Lockout")]
        public IActionResult Lockout()
        {
            return View();
        }

        [HttpGet("Exists")]
        public IActionResult Exists()
        {
            return View();
        }

        [HttpGet("NoExists")]
        public IActionResult NoExists()
        {
            return View();
        }
    }
}
