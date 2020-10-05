using Microsoft.AspNetCore.Mvc;

namespace AspnetCore.Configuration.Samples.Controllers
{
    public class DefaultController : Controller
    {
        public string Index() =>
            "Hello World!";
    }
}
