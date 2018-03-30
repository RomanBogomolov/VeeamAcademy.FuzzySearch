using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamAcademy.FuzzySearch.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var distance = new CFuzzySearch();
            var b = new List<Tuple<string, string>>();
            b.Add(new Tuple<string, string>("привет",""));
            b.Add(new Tuple<string, string>("пока",""));
            b.Add(new Tuple<string, string>("privet",""));
            distance.SetData(b);
            var a = distance.Search("прив");
            
        }
    }
}
