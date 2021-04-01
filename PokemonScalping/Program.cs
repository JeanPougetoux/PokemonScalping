using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Chrome;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PokemonScalping
{
    class Program
    {
        private static List<Item> picWikItems = new List<Item>();
        private static List<Item> kingJouetItems = new List<Item>();
        private static string[] picWikStores = new string[]
        {
            "STG",
            "ORM",
            "CAR",
            "DEF",
            "PAR",
            "LGA",
            "VSD"
        };
        private static Dictionary<string, string> picWikStoresMapping = new Dictionary<string, string>
        {
            ["STG"] = "Saint-Genevieve des bois",
            ["ORM"] = "Ormesson",
            ["CAR"] = "Carré Sénart",
            ["DEF"] = "La Défense",
            ["PAR"] = "O'Parinor",
            ["LGA"] = "Livry Gargan",
            ["VSD"] = "Vert Sain-Denis"
        };
        private static string[] kingJouetStores = new string[]
        {
            "0701",
            "0269",
            "0267",
            "0266"
        };
        private static Dictionary<string, string> kingJouetStoresMapping = new Dictionary<string, string>
        {
            ["0701"] = "KING JOUET / ORCHESTRA THIAIS",
            ["0269"] = "KING JOUET STE GENEVIEVE DES BOIS",
            ["0267"] = "KING JOUET PARIS SAINT MICHEL",
            ["0266"] = "KING JOUET PARIS RIVOLI",
        };

        static void Main(string[] args)
        {
            if(File.Exists(@".\picwiktoys.json"))
            {
                picWikItems = JsonConvert.DeserializeObject<List<Item>>(File.ReadAllText(@".\picwiktoys.json"));
            }
            if(File.Exists(@".\kingjouet.json"))
            {
                kingJouetItems = JsonConvert.DeserializeObject<List<Item>>(File.ReadAllText(@".\kingjouet.json"));
            }

            while(true)
            {
                CrawlPicWikToys();
                CrawlKingJouet();

                Thread.Sleep(3600000);
            }
        }

        private static void CrawlPicWikToys()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"--------- NOUVEAU CRAWL POUR PICWIKTOYS ({DateTime.Now}) ---------");
            var actual = InitItemsForPicwiktoys().ToList();
            SynchroPicWikToys(actual);
            Console.WriteLine();
            File.WriteAllText(@".\picwiktoys.json", JsonConvert.SerializeObject(picWikItems));
        }

        private static void CrawlKingJouet()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"--------- NOUVEAU CRAWL POUR KINGJOUET ({DateTime.Now}) ---------");
            var actual = InitAllForKingJouet().ToList();
            SynchroKingJouet(actual);
            Console.WriteLine();
            File.WriteAllText(@".\kingjouet.json", JsonConvert.SerializeObject(kingJouetItems));
        }

        private static void SynchroKingJouet(List<Item> actual)
        {
            for (var i = kingJouetItems.Count - 1; i > 0; i--)
            {
                var actualItem = actual.FirstOrDefault(a => a.Id == kingJouetItems[i].Id);

                if (actualItem == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Suppression d'item chez kingjouet ! {kingJouetItems[i].Name}, id : {kingJouetItems[i].Id}");
                    kingJouetItems.RemoveAt(i);
                }
                else
                {
                    for (var j = kingJouetItems[i].Stocks.Count - 1; j > 0; j--)
                    {
                        var actualStock = actualItem.Stocks.FirstOrDefault(s => s.StoreId == kingJouetItems[i].Stocks[j].StoreId);

                        if (actualStock == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Plus de stock pour l'article {kingJouetItems[i].Name} pour le magasin kingjouet {kingJouetStoresMapping[kingJouetItems[i].Stocks[j].StoreId]}.");
                            kingJouetItems[i].Stocks.RemoveAt(j);
                        }
                    }
                }
            }

            foreach (var element in actual)
            {
                var kingJouetElement = kingJouetItems.FirstOrDefault(pwi => pwi.Id == element.Id);

                if (kingJouetElement == null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Nouvel item chez kingjouet ! {element.Name}, id : {element.Id}");

                    foreach (var st in element.Stocks)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Ajout de stock pour l'article {element.Name} pour le magasin kingjouet {kingJouetStoresMapping[st.StoreId]}.");
                    }

                    if (element.AvalaibleWeb)
                    {
                        Console.WriteLine($"Ajout de stock web pour l'article {element.Name} pour kingjouet.");
                    }

                    kingJouetItems.Add(new Item(element.Name, element.Id, element.Stocks, element.AvalaibleWeb));
                }
                else
                {
                    if(kingJouetElement.AvalaibleWeb != element.AvalaibleWeb)
                    {
                        if(kingJouetElement.AvalaibleWeb)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Plus de stock web pour l'article {element.Name} pour kingjouet.");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Ajout de stock web pour l'article {element.Name} pour kingjouet.");
                        }

                        kingJouetElement.AvalaibleWeb = element.AvalaibleWeb;
                    }

                    foreach (var st in element.Stocks)
                    {
                        var existingStock = kingJouetElement.Stocks.FirstOrDefault(s => s.StoreId == st.StoreId);

                        if (existingStock == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Ajout de stock pour l'article {element.Name} pour le magasin kingjouet {kingJouetStoresMapping[st.StoreId]}.");
                            kingJouetElement.Stocks.Add(new Stock(st.StoreId, st.ItemNumber));
                        }
                    }
                }
            }
        }

        private static IEnumerable<Item> InitAllForKingJouet()
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://yj3fse95rz-dsn.algolia.net/1/indexes/*/queries?x-algolia-agent=Algolia%20for%20JavaScript%20(3.33.0)%3B%20Browser%20(lite)%3B%20instantsearch.js%20(4.2.0)%3B%20JS%20Helper%20(3.1.0)%3B%20autocomplete.js%200.38.0&x-algolia-application-id=YJ3FSE95RZ&x-algolia-api-key=b336dfcccca85c6a9233bf63944da4cb");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                JObject json = new JObject
                {
                    ["requests"] = new JArray
                    {
                        new JObject
                        {
                            ["indexName"] = "KingJouetCom_PROD",
                            ["params"] = "filters=codeMag%3A%22WEB%22%20OR%20codeMag%3A%22WEB%22&clickAnalytics=true&query=carte%20pokemon&maxValuesPerFacet=20&highlightPreTag=__ais-highlight__&highlightPostTag=__%2Fais-highlight__&page=0&facets=%5B%22marque%22%2C%22keywords%22%2C%22licence%22%2C%22notePourFacette%22%2C%22ages%22%2C%22categories.lvl0%22%5D&tagFilters=",
                        }
                    }
                };

                streamWriter.Write(json);
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                JObject converted = JObject.Parse(result);

                var articles = converted["results"][0]["hits"];

                foreach(var article in articles)
                {
                    var guid = article["guid"];
                    var name = article["libelle"];
                    var web = (bool)article["estDispoWeb"];
                    var mag = article["etatMag"]
                        .Select(em => em.ToString().Split("_")[0])
                        .Distinct()
                        .Where(em => kingJouetStores.Contains(em))
                        .ToList();

                    yield return new Item(name.ToString(), guid.ToString(), mag.Select(m => new Stock(m, 1)).ToList(), web);
                }
            }
        }

        private static void SynchroPicWikToys(List<Item> actual)
        {
            for (var i = picWikItems.Count - 1; i > 0; i--)
            {
                var actualItem = actual.FirstOrDefault(a => a.Id == picWikItems[i].Id);

                if (actualItem == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Suppression d'item chez picwiktoys ! {picWikItems[i].Name}, sku : {picWikItems[i].Id}");
                    picWikItems.RemoveAt(i);
                }
                else
                {
                    for (var j = picWikItems[i].Stocks.Count - 1; j > 0; j--)
                    {
                        var actualStock = actualItem.Stocks.FirstOrDefault(s => s.StoreId == picWikItems[i].Stocks[j].StoreId);

                        if (actualStock == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Changement de stock pour l'article {picWikItems[i].Name} pour le magasin picwiktoys {picWikStoresMapping[picWikItems[i].Stocks[j].StoreId]} : ancien - {picWikItems[i].Stocks[j].ItemNumber}, nouveau - 0");
                            picWikItems[i].Stocks.RemoveAt(j);
                        }
                    }
                }
            }

            foreach (var element in actual)
            {
                var picWikElement = picWikItems.FirstOrDefault(pwi => pwi.Id == element.Id);

                if (picWikElement == null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Nouvel item chez picwiktoys ! {element.Name}, sku : {element.Id}");

                    foreach(var st in element.Stocks)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Ajout de stock pour l'article {element.Name} pour le magasin picwiktoys {picWikStoresMapping[st.StoreId]} : {st.ItemNumber} items.");
                    }

                    if(element.AvalaibleWeb)
                    {
                        Console.WriteLine($"Ajout de stock web pour l'article {element.Name} pour picwiktoys.");
                    }

                    picWikItems.Add(new Item(element.Name, element.Id, element.Stocks, element.AvalaibleWeb));
                }
                else
                {
                    if (picWikElement.AvalaibleWeb != element.AvalaibleWeb)
                    {
                        if (picWikElement.AvalaibleWeb)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Plus de stock web pour l'article {element.Name} pour picwiktoys.");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Ajout de stock web pour l'article {element.Name} pour picwiktoys.");
                        }

                        picWikElement.AvalaibleWeb = element.AvalaibleWeb;
                    }

                    foreach (var st in element.Stocks)
                    {
                        var existingStock = picWikElement.Stocks.FirstOrDefault(s => s.StoreId == st.StoreId);

                        if (existingStock == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Ajout de stock pour l'article {element.Name} pour le magasin picwiktoys {picWikStoresMapping[st.StoreId]} : {st.ItemNumber} items.");
                            picWikElement.Stocks.Add(new Stock(st.StoreId, st.ItemNumber));
                        } 
                        else if (existingStock.ItemNumber != st.ItemNumber)
                        {
                            Console.ForegroundColor = existingStock.ItemNumber < st.ItemNumber ? ConsoleColor.Green : ConsoleColor.Yellow;
                            Console.WriteLine($"Changement de stock pour l'article {element.Name} pour le magasin picwiktoys {picWikStoresMapping[st.StoreId]} : ancien - {existingStock.ItemNumber}, nouveau - {st.ItemNumber}");
                            existingStock.ItemNumber = st.ItemNumber;
                        }
                    }
                }
            }
        }

        private static (IEnumerable<Stock>, bool) InitStockForPicwiktoys(string sku)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://api.picwictoys.com/api/product_stock");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            var stocks = new List<Stock>();

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                JObject json = new JObject
                {
                    ["ref_product"] = $"{sku}"
                };

                streamWriter.Write(json);
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                JObject converted = JObject.Parse(result);
                var allStock = converted["allStock"];
                var web = (int)converted["web"]["stock"] > 0;
                foreach(JProperty stock in allStock)
                {
                    var storeId = stock.Name.ToString();
                    if (!picWikStores.Contains(storeId)) continue;
                    var storeQty = (int)stock.Value;

                    stocks.Add(new Stock(storeId, storeQty));
                }

                return (stocks, web);
            }
        }

        private static IEnumerable<Item> InitItemsForPicwiktoys()
        {
            int i = 1;

            while (true)
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://pictoys-prod.prediggo.io/ptoys-prod-JS-WLzZEeEQpU84gJ7yHCAv/3.0/simplePageContent");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    JObject json = new JObject
                    {
                        ["moduleVersion"] = "production",
                        ["sessionId"] = "ok",
                        ["predSessionId"] = "ok",
                        ["region"] = "fr_FR",
                        ["pageId"] = 0,
                        ["blockId"] = 0,
                        ["parameters"] = new JObject
                        {
                            ["filters"] = new JObject
                            {
                                ["category"] = new JArray
                                {
                                    "153"
                                },
                                ["C_MARQUE1"] = new JArray
                                {
                                    "Asmodée"
                                }
                            },
                            ["page"] = i
                        }
                    };

                    streamWriter.Write(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    JObject converted = JObject.Parse(result);

                    if (converted["pageName"].ToString() == "0 résults") break;

                    JArray items = (JArray)converted["blocks"]["searches"][0]["slots"];

                    if (items.Count == 0) break;

                    foreach (var block in items)
                    {
                        var item = block["item"];
                        var sku = item["sku"].ToString();
                        var name = item["attributeInfo"].First(ai => ai["attributeName"].ToString() == "LIBWEB")["vals"].First()["value"].ToString();

                        var stocks = InitStockForPicwiktoys(sku);

                        yield return new Item(name, sku, stocks.Item1.ToList(), stocks.Item2);
                    }
                }
                i++;
            }
        }
    }
}
