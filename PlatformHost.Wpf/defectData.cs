using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp2.Models
{
    public class DefectData
    {
        public List<string> ImagePath { get; set; } //每张图的地址
        public Dictionary<string, double[]> Tabs { get; set; } // 每个 Tab 的数据
    }
}
