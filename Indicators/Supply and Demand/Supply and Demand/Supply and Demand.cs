using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;
using System.Collections.Generic;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SupplyandDemand : Indicator
    {
        [Parameter("Period", DefaultValue = 5)]
        public int per { get; set; }
        
        [Parameter("Supply Color", DefaultValue = "Red")]
        public string sColor { get; set; }

        [Parameter("Demand Color", DefaultValue = "Lime")]
        public string dColor { get; set; }

        [Parameter("Opacity %", DefaultValue = 20)]
        public int opt { get; set; }

        [Output("Main")]
        public IndicatorDataSeries Result { get; set; }

        private Bars series;
        private List<Supply> sList = new List<Supply>();
        private List<Demand> dList = new List<Demand>();
        private Color AsColor, AdColor;

        protected override void Initialize()
        {
        try
        {
            series = MarketData.GetBars(this.TimeFrame);
            AsColor = Color.FromArgb((int)(255 * 0.01 * opt), Color.FromName(sColor).R, Color.FromName(sColor).G, Color.FromName(sColor).B);
            AdColor = Color.FromArgb((int)(255 * 0.01 * opt), Color.FromName(dColor).R, Color.FromName(dColor).G, Color.FromName(dColor).B);
            
            Print("Version 1.0, community indicator, contact ClickAlgo.com for custom development work.");
            
         }catch(Exception ex) {Print(ex.Message);}
        }

        public override void Calculate(int index)
        {
            var index2 = series.OpenTimes.GetIndexByExactTime(Bars.OpenTimes[index]);
            var index3 = series.OpenTimes.GetIndexByExactTime(series.OpenTimes[index2 - per]);

            bool s = true;
            bool t = true;

            //SUPPLY

            for (int i = 1; i < per; i++)
            {
                if (series.HighPrices[index2 - per + i] > series.HighPrices[index2 - per] && s == true)
                {
                    s = false;
                }
            }
            for (int i = 1; i < per; i++)
            {
                if (series.HighPrices[index2 - per - i] > series.HighPrices[index2 - per] && s == true)
                {
                    s = false;
                }
            }
            if (s == true)
            {
                sList.Add(new Supply(index3, series.HighPrices[index2 - per]));
            }

            //DEMAND
            for (int i = 1; i < per; i++)
            {
                if (series.LowPrices[index2 - per + i] < series.LowPrices[index2 - per] && t == true)
                {
                    t = false;
                }

            }
            for (int i = 1; i < per; i++)
            {
                if (series.LowPrices[index2 - per - i] < series.LowPrices[index2 - per] && t == true)
                {
                    t = false;
                }

            }
            if (t == true)
            {
                dList.Add(new Demand(index3, series.LowPrices[index2 - per]));
            }


            if (!IsLastBar)
                return;

            //DRAWING
            foreach (var zone in sList)
            {
                for (int i = zone.index; i < index; i++)
                {
                    if (Bars.HighPrices[i] > zone.high)
                    {
                        int fIndex = series.OpenTimes.GetIndexByTime(Bars.OpenTimes[zone.index]);
                        Chart.DrawRectangle("supply" + zone.index + " " + i, zone.index, Math.Max(series.ClosePrices[fIndex], series.OpenPrices[fIndex]), i, zone.high, AsColor, 1).IsFilled = true;
                        break;
                    }
                }
            }

            foreach (var zone in dList)
            {
                for (int i = zone.index; i < index; i++)
                {
                    if (Bars.LowPrices[i] < zone.low)
                    {
                        int fIndex = series.OpenTimes.GetIndexByTime(Bars.OpenTimes[zone.index]);
                        Chart.DrawRectangle("demand" + zone.index + " " + i, zone.index, Math.Min(series.ClosePrices[fIndex], series.OpenPrices[fIndex]), i, zone.low, AdColor, 1).IsFilled = true;
                        break;
                    }
                }
            }
        }
    }

    public class Supply
    {

        public int index;
        public double high;

        public Supply(int index, double high)
        {
            this.index = index;
            this.high = high;
        }

    }

    public class Demand
    {

        public int index;
        public double low;

        public Demand(int index, double low)
        {
            this.index = index;
            this.low = low;
        }

    }
}
