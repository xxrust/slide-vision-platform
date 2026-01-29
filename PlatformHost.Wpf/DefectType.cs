using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp2.Models
{
    public class DefectType
    {
        public string Name { get; set; }
        public List<string> Tabs { get; set; }
        public string ImagePath { get; set; }
        public Dictionary<string, double[]> TabRanges { get; set; } // 新增：存储每个 Tab 的 [X1, X2]
    }
}
