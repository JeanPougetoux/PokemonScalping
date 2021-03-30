using System;
using System.Collections.Generic;
using System.Text;

namespace PokemonScalping
{
    public class Stock
    {
        public Stock(string storeId, int itemNumber)
        {
            StoreId = storeId;
            ItemNumber = itemNumber;
        }

        public string StoreId { get; set; }
        public int ItemNumber { get; set; }
    }
}
