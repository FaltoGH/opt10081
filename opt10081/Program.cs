using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using AxKHOpenAPILib;

namespace opt10081
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var a = new AxKHOpenAPI();
            new Control().Controls.Add(a);
            a.EndInit();
            if(a.CommConnect() == 0)
            {
                
            }
        }
    }
}
