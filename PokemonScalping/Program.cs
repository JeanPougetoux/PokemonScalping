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
            InitItemsForPicwiktoys();
            InitStockForPicwiktoys();
            Console.WriteLine();
        }

        private static void InitStockForPicwiktoys()
        {
            foreach (var item in picWikItems)
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://api.picwictoys.com/api/product_stock");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    JObject json = new JObject
                    {
                        ["ref_product"] = $"{item.Id}"
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

                        var store = item.Stocks.FirstOrDefault(stock => stock.StoreId == storeId);

                        if (store != null)
                        {
                            if(store.ItemNumber != storeQty)
                            {
                                Console.WriteLine($"Changement de stock pour l'article {item.Name} pour le magasin picwiktoys {picWikStoresMapping[storeId]} : ancien - {store.ItemNumber}, nouveau - {storeQty}");
                                store.ItemNumber = storeQty;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Ajout de stock pour l'article {item.Name} pour le magasin picwiktoys {picWikStoresMapping[storeId]} : {storeQty} items.");
                            item.Stocks.Add(new Stock(storeId, storeQty));
                        }
                    }
                }
            }
        }

        private static void InitItemsForPicwiktoys()
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

                        if (!picWikItems.Any(item => item.Id == sku))
                        {
                            Console.WriteLine($"Nouvel item chez picwiktoys ! {name}, sku : {sku}");
                            picWikItems.Add(new Item(name, sku, new List<Stock>()));
                        }
                    }
                }
                i++;
            }
        }
    }
}
