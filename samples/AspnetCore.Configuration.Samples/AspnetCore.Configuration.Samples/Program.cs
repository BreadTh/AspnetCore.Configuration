using BreadTh.AspNet.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspnetCore.Configuration.Samples
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = new StandardHost<Startup>().Build(args: args);
            host.Run();
        }
    }
}