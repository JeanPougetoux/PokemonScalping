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

        static void Main(string[] args)
        {
            if(File.Exists(@".\picwiktoys.json"))
            {
                picWikItems = JsonConvert.DeserializeObject<List<Item>>(File.ReadAllText(@".\picwiktoys.json"));
            }

            while(true)
            {
                CrawlPicWikToys();
                File.WriteAllText(@".\picwiktoys.json", JsonConvert.SerializeObject(picWikItems));

                Thread.Sleep(3600000);
            }
        }

        private static void CrawlPicWikToys()
        {
            Console.WriteLine();
            Console.WriteLine($"--------- NOUVEAU CRAWL POUR PICWIKTOYS ({DateTime.Now}) ---------");
            var actual = InitItemsForPicwiktoys().ToList();
            Synchro(actual);
            Console.WriteLine();
        }

        private static void Synchro(List<Item> actual)
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
                        Console.WriteLine($"Ajout de stock pour l'article {element.Name} pour le magasin picwiktoys {st.StoreId} : {st.ItemNumber} items.");
                    }
                    picWikItems.Add(new Item(element.Name, element.Id, element.Stocks));
                }
                else
                {
                    foreach(var st in element.Stocks)
                    {
                        var existingStock = picWikElement.Stocks.FirstOrDefault(s => s.StoreId == st.StoreId);

                        if (existingStock == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Ajout de stock pour l'article {element.Name} pour le magasin picwiktoys {st.StoreId} : {st.ItemNumber} items.");
                            picWikElement.Stocks.Add(new Stock(st.StoreId, st.ItemNumber));
                        } 
                        else if (existingStock.ItemNumber != st.ItemNumber)
                        {
                            Console.ForegroundColor = existingStock.ItemNumber < st.ItemNumber ? ConsoleColor.Green : ConsoleColor.Yellow;
                            Console.WriteLine($"Changement de stock pour l'article {element.Name} pour le magasin picwiktoys {st.StoreId} : ancien - {existingStock.ItemNumber}, nouveau - {st.ItemNumber}");
                            existingStock.ItemNumber = st.ItemNumber;
                        }
                    }
                }
            }
        }

        private static IEnumerable<Stock> InitStockForPicwiktoys(string sku)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://api.picwictoys.com/api/product_stock");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

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
                foreach(JProperty stock in allStock)
                {
                    var storeId = stock.Name.ToString();
                    if (!picWikStores.Contains(storeId)) continue;
                    var storeQty = (int)stock.Value;

                    yield return new Stock(storeId, storeQty);
                }
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

                        yield return new Item(name, sku, stocks.ToList());
                    }
                }
                i++;
            }
        }
    }
}
