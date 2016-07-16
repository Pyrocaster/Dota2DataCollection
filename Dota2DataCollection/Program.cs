using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Liquidpedia;
using System.Configuration;


namespace Dota2DataCollection
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
