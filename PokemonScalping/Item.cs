using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonScalping
{
    public class Item
    {
        public Item(string name, string id, List<Stock> stocks, bool avalaibleWeb = false)
        {
            Name = name;
            Id = id;
            AvalaibleWeb = avalaibleWeb;
            Stocks = stocks;
        }

        public string Name { get; set; }
        public string Id { get; set; }
        public bool AvalaibleWeb { get; set; }
        public List<Stock> Stocks { get; set; }
    }
}
