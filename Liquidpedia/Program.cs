using System.Net.Http;
using System.Xml.Linq;
using System.Configuration;

namespace Liquidpedia
{
    class Program
    {
        static void Main(string[] args)
        {
            HeroRoleScraper p = new HeroRoleScraper();
            var directory = ConfigurationManager.AppSettings.Get("localXMLStore");
            p.GetData(directory);
        }
    }
}