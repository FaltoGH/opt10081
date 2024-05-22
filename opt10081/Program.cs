using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                a.OnEventConnect += (o, e) =>
                {
                    Func<string, HashSet<string>> f = x => a.GetCodeListByMarket(x).Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

                    var target = new HashSet<string>();

                    var kospi = f("0");
                    var kosdaq = f("10");
                    var elw = f("3");
                    var etf = f("8");
                    var konex = f("50");
                    var mutualfund = f("4");
                    var warrant = f("5");
                    var ritz = f("6");
                    var highyieldfund = f("9");
                    var kotc = f("30");

                    target.UnionWith(kospi);
                    target.UnionWith(kosdaq);

                    target.ExceptWith(elw);
                    target.ExceptWith(etf);
                    target.ExceptWith(konex);
                    target.ExceptWith(mutualfund);
                    target.ExceptWith(warrant);
                    target.ExceptWith(ritz);
                    target.ExceptWith(highyieldfund);
                    target.ExceptWith(kotc);

                    // 보통주만 남기기
                    target.RemoveWhere(x => x[5] != '0');

                    // 스팩주 제외
                    target.RemoveWhere(x => a.GetMasterCodeName(x).Contains("스팩"));

                    // ETN 제외
                    target.RemoveWhere(x => a.KOA_Functions("GetStockMarketKind", x) == "60");

                    // 정리매매 제외
                    target.RemoveWhere(x => a.KOA_Functions("IsOrderWarningStock", x) == "2");

                    Console.WriteLine("target.Count="+target.Count);

                };

                while (true)
                {
                    Thread.Sleep(1);
                    Application.DoEvents();
                }
            }
        }
    }
}
