using Microsoft.AspNetCore.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Google.ViewModels
{
    public class LoginModel
    {
        public List<AuthenticationScheme> AuthenticationSchemes { get; set; }
        public string ReturnUrl { get; set; } 
    }
}
