using System;
using System.Net.Http;
using System.Xml.Linq;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Liquidpedia
{
    class HeroRoleScraper
    {
        /// <summary>
        /// Scrape wiki.teamliquid.net to and output XML with hero and hero role metadata in an easily consumable format, by default writes to XML directory found in App.config
        /// </summary>
        public void GetData(string outputDirectory = null)
        {
            var doc = GetHeroRoleWebDoc();
            var data = ParseHeroRoleDoc(doc);
            SaveHeroRoleXML(data);
        }



        /// <summary>
        /// Returns an Xdocument that contains role data from liquidpedia
        /// </summary>
        /// <returns></returns>
        private XDocument GetHeroRoleWebDoc()
        {
            using (HttpClient client = new HttpClient())
            {
                string baseUri = ConfigurationManager.AppSettings.Get("LiquidpediaBaseURI");
                string heroRoleUri = ConfigurationManager.AppSettings.Get("HeroRolesURI");
                XDocument doc = null;

                //Add retry logic here in the future
                //Converting async call to synchronous to keep code simple and avoid unnessecary async behaviour in the main thread
                HttpResponseMessage response = client.GetAsync(baseUri + heroRoleUri).Result;

                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsStringAsync().Result;
                    doc = XDocument.Parse(content);
                }
                return doc;
            }
        }



        /// <summary>
        ///Parses an Xdocument that has hero role data encoded and returns a dictionary containg roles and corresponding rating out of 3 for every hero in Dota2
        ///This implementation is tightly bound to website format and layout as of 7/7/2016
        /// </summary>
        /// <param name="doc">XDocument to be parsed</param>
        /// <returns></returns>
        private SortedDictionary<string, List<RoleRating>> ParseHeroRoleDoc(XDocument doc)
        {
            //Initialize data
            //Get all table elements and their descendants
            var xmlTables = doc.Root.Descendants("table").DescendantsAndSelf();
            int currentRating = -1;
            var currentRole = "";

            SortedDictionary<string, List<RoleRating>> data = new SortedDictionary<string, List<RoleRating>>();


            //Parse the input XML
            foreach (XElement e in xmlTables)
            {
                //Extracting the role and rating data from the <th><th/> (table header) elements
                if (e.Name == "th")
                {
                    var roleRating = e.Value;
                    var s = roleRating.Split(' ');
                    currentRating = s[1].Length;
                    currentRole = s[4].TrimEnd();
                }
                //Extracting hero data that corresponds to current role and rating based on display order in the website table from <a></a> elements
                if (e.Name == "a")
                {
                    var heroName = e.Attribute("title") == null ? "" : e.Attribute("title").Value;

                    //create list for the hero in the dictionary, if it doesnt exist for the hero and ignore blanks
                    if (data.ContainsKey(heroName) && heroName != "")
                    {
                        data[heroName].Add(new RoleRating(currentRole, currentRating));
                    }
                    else if (heroName != "")
                    {
                        List<RoleRating> list = new List<RoleRating>();
                        list.Add(new RoleRating(currentRole, currentRating));
                        data[heroName] = list;
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            return data;
        }



        /// <summary>
        /// Writes formatted XML file of the format 'HeroRole_<YearMonthDay>_<HourMinute>' containing hero and role metadata to specified output directory, 
        /// By default will write to localXMLStore location set in App.config
        /// </summary>
        /// <param name="data">Dictionary with Hero Role data</param>
        /// <param name="outputDirectory">Location to write XML</param>
        private void SaveHeroRoleXML(SortedDictionary<string, List<RoleRating>> data, string outputDirectory = null)
        {
            //If output directory not provided, write to "localXMLStore" 
            if (outputDirectory == null)
            {
               var xmlDir = ConfigurationManager.AppSettings.Get("HeroRoleXMLStore_SriniLocal");
                outputDirectory = xmlDir;
            }

            XDocument doc = new XDocument(new XElement("HeroList"));

            //Loop over each hero and add to the XML document
            foreach (var i in data)
            {
                //key of the dictionary has hero name
                var hero = i.Key;
                //Add Hero node
                doc.Root.Add(new XElement("Hero", new XAttribute("Name", hero),
                                new XElement("Roles")));

                //Sorting RoleRatings by rating to have maximum rating first in XML - this is cosmetic ONLY, remove if causing perf issue
                data[hero].Sort(delegate (RoleRating r1, RoleRating r2) { return (r2.rating.CompareTo(r1.rating)); });
                //Loop over the roles and ratings for each hero and add to the XML document
                foreach (var k in data[hero])
                {
                    var role = k.role;
                    var rating = k.rating;
                    //Get correct <hero/> node corresponding to hero name
                    var h = (from nodes in doc.Root.Descendants("Hero")
                             where nodes.Attribute("Name").Value == hero
                             select nodes).FirstOrDefault();
                    //Get <roles/> child node from hero node
                    var r = h.Descendants("Roles").FirstOrDefault();
                    //add role and rating data
                    r.Add(new XElement("Role", new XAttribute("Type", role), new XAttribute("Rating", rating)));
                }
            }

            var filename = "\\HeroRoles_" + DateTime.UtcNow.Year + DateTime.UtcNow.Month + DateTime.UtcNow.Day + "_" + DateTime.UtcNow.Hour + DateTime.UtcNow.Minute + ".xml";
            doc.Save(outputDirectory + filename);
        }



        /// <summary>
        /// Private class used for parsing
        /// </summary>
        private class RoleRating
        {
            public RoleRating(string ro, int ra)
            {
                role = ro;
                rating = ra;
            }

            public string role { get; set; }
            public int rating { get; set; }
        }
    }
}
