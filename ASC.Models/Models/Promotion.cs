﻿using ASC.Models.BaseTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace ASC.Models.Models
{
    public class Promotion : BaseEntity
    {
        public Promotion() { }
        public Promotion(string type)
        {
            this.RowKey = Guid.NewGuid().ToString();
            this.PartitionKey = type;
        }

        public string Header { get; set; }
        public string Content { get; set; }
    }
}
