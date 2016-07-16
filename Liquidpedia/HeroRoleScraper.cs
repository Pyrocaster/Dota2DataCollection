using System;
using System.Net.Http;
using System.Xml.Linq;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Net.Mail;
using SharedResources;

namespace Liquidpedia
{
    public class HeroRoleScraper
    {
        /// <summary>
        /// Scrape wiki.teamliquid.net to and output XML with hero and hero role metadata in an easily consumable format.
        /// Incorporates retry logic, and will send email to mailing list found in App.config if XML cannot be created.
        /// </summary>
        /// <param name="outputDirectory">Directory to which HeroRole XML should be written, default found in App.config</param>
        public void GetData(string outputDirectory = null)
        {
            var doc = GetHeroRoleWebDoc();
            var data = ParseHeroRoleDoc(doc);
            SaveHeroRoleXML(data);
        }



        /// <summary>
        /// Returns an Xdocument that contains role data from liquidpedia. Will send email if data retrieval from website fails
        /// </summary>
        /// <returns></returns>
        private XDocument GetHeroRoleWebDoc()
        {
            using (HttpClient client = new HttpClient())
            {
                var baseUri = ConfigurationManager.AppSettings.Get("LiquidpediaBaseURI");
                var heroRoleUri = ConfigurationManager.AppSettings.Get("HeroRolesURI");
                var retryCount = int.Parse(ConfigurationManager.AppSettings.Get("RetryCount"));
                var delay = int.Parse(ConfigurationManager.AppSettings.Get("DelayCount"));
                var currentAttempt = 0;
                var body = "Failure in getting a successful (200) response from Liquidpedia for hero role data. URL queried: " + baseUri + heroRoleUri + ". ";
                var subject = "Liquidpedia Data Scrape Failure";
                XDocument doc = null;
                HttpResponseMessage response = null;
                

                //Get liquidpeida page, attempt to retry with exponential delay for unsuccessful responses or transient failures
                while (currentAttempt <= retryCount)
                {
                    try
                    {
                        //get liquiedpedia resource
                        response = client.GetAsync(baseUri + heroRoleUri).Result;

                        //if response is successful get data
                        if (response.IsSuccessStatusCode)
                        {
                            var content = response.Content.ReadAsStringAsync().Result;
                            doc = XDocument.Parse(content);
                            break;
                        }
                        //if response is not successful, increment current attempt and retry
                        else
                        {
                            //increment attempt number
                            currentAttempt++;
                            body = body + "Additonal details: Unsucessful Responses";
                            //Add exponential delay
                            Thread.Sleep((int)(delay * (Math.Pow(currentAttempt, 2))));
                        }
                    }
                    catch (Exception ex)
                    {
                        //increment attempt number
                        currentAttempt++;
                        body = body + "Additonal details: Exceptions thrown, potential network failure";
                        //Add exponential delay
                        Thread.Sleep((int)(delay * (Math.Pow(currentAttempt, 2))));
                    }


                    //if all retry attempts are over, log send email notification
                    if (currentAttempt == retryCount)
                    {
                        //send email that hero role generation has failed
                        Utilties u = new Utilties();
                        u.SendEmail(subject, body);
                        //log and throw exception
                        Exception ex = new Exception("Data retrieval from Liquidpedia failed, refer logs for more details");
                        throw ex;                      
                    }
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
