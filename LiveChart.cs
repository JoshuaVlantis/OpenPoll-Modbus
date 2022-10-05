using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Definitions.Charts;
using LiveCharts.WinForms;
using LiveCharts.Wpf;

namespace EasyBus_Modbus_Scanner
{
    public partial class LiveChart : Form
    {
        int RowIndex;
        int ColumnIndex;
        int RowIndex1;
        int ColumnIndex1;
        int RowIndex2;
        int ColumnIndex2;
        int RowIndex3;
        int ColumnIndex3;
        int RowIndex4;
        int ColumnIndex4;
        int index = 0;

        public LiveChart(int RowIndex, int ColumnIndex)
        {
            this.RowIndex = RowIndex;
            this.ColumnIndex = ColumnIndex;

            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            //the ChartValues property will store our values array
            ChartValue = new ChartValues<MeasureModel>();
            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex),
                    Values = ChartValue,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },
            };

            cartesianChart1.DisableAnimations = true;
            cartesianChart1.DataTooltip = null;
            cartesianChart1.Hoverable = false;
            cartesianChart1.LegendLocation = LegendLocation.Right;
            cartesianChart1.DefaultLegend.Visibility = Visibility.Visible;

            cartesianChart1.AxisX.Add(new Axis
            {
                DisableAnimations = true,
                LabelFormatter = value => new System.DateTime((long)value).ToString("mm:ss"),
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(1).Ticks
                }
            });

            var now = System.DateTime.Now;
            cartesianChart1.AxisX[0].MinValue = now.Ticks;
            SetAxisLimits(System.DateTime.Now);

            //The next code simulates data changes every 500 ms
            Timer = new Timer
            {
                Interval = 1000
            };
            Timer.Tick += TimerOnTick;
            Timer.Start();
        }

        // 2 series
        public LiveChart(int RowIndex, int ColumnIndex, int RowIndex1, int ColumnIndex1)
        {
            this.RowIndex1 = RowIndex1;
            this.ColumnIndex1 = ColumnIndex1;
            this.ColumnIndex = ColumnIndex;
            this.RowIndex = RowIndex;

            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            //the ChartValues property will store our values array
            ChartValue = new ChartValues<MeasureModel>();
            ChartValue2 = new ChartValues<MeasureModel>();
            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex),
                    Values = ChartValue,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex1),
                    Values = ChartValue2,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                }
            };

            cartesianChart1.DisableAnimations = true;
            cartesianChart1.DataTooltip = null;
            cartesianChart1.Hoverable = false;
            cartesianChart1.LegendLocation = LegendLocation.Right;
            cartesianChart1.DefaultLegend.Visibility = Visibility.Visible;

            cartesianChart1.AxisX.Add(new Axis
            {
                DisableAnimations = true,
                LabelFormatter = value => new System.DateTime((long)value).ToString("mm:ss"),
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(1).Ticks
                }
            });

            var now = System.DateTime.Now;
            cartesianChart1.AxisX[0].MinValue = now.Ticks;
            SetAxisLimits(System.DateTime.Now);

            //The next code simulates data changes every 500 ms
            Timer = new Timer
            {
                Interval = 1000
            };
            Timer.Tick += TimerOnTick1;
            Timer.Start();
        }

        //3
        public LiveChart(int RowIndex, int ColumnIndex, int RowIndex1, int ColumnIndex1, int RowIndex2, int ColumnIndex2)
        {
            this.RowIndex1 = RowIndex1;
            this.ColumnIndex1 = ColumnIndex1;
            this.ColumnIndex = ColumnIndex;
            this.RowIndex = RowIndex;
            this.RowIndex2 = RowIndex2;
            this.ColumnIndex2 = ColumnIndex2;

            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            //the ChartValues property will store our values array
            ChartValue = new ChartValues<MeasureModel>();
            ChartValue2 = new ChartValues<MeasureModel>();
            ChartValue3 = new ChartValues<MeasureModel>();
            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex),
                    Values = ChartValue,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex1),
                    Values = ChartValue2,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex2),
                    Values = ChartValue3,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                }
            };

            cartesianChart1.DisableAnimations = true;
            cartesianChart1.DataTooltip = null;
            cartesianChart1.Hoverable = false;
            cartesianChart1.LegendLocation = LegendLocation.Right;
            cartesianChart1.DefaultLegend.Visibility = Visibility.Visible;

            cartesianChart1.AxisX.Add(new Axis
            {
                DisableAnimations = true,
                LabelFormatter = value => new System.DateTime((long)value).ToString("mm:ss"),
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(1).Ticks
                }
            });

            var now = System.DateTime.Now;
            cartesianChart1.AxisX[0].MinValue = now.Ticks;
            SetAxisLimits(System.DateTime.Now);

            //The next code simulates data changes every 500 ms
            Timer = new Timer
            {
                Interval = 1000
            };
            Timer.Tick += TimerOnTick2;
            Timer.Start();
        }

        //4
        public LiveChart(int RowIndex, int ColumnIndex, int RowIndex1, int ColumnIndex1, int RowIndex2, int ColumnIndex2, int RowIndex3, int ColumnIndex3)
        {
            this.RowIndex1 = RowIndex1;
            this.ColumnIndex1 = ColumnIndex1;
            this.ColumnIndex = ColumnIndex;
            this.RowIndex = RowIndex;
            this.RowIndex2 = RowIndex2;
            this.ColumnIndex2 = ColumnIndex2;
            this.ColumnIndex3 = ColumnIndex3;
            this.RowIndex3 = RowIndex3;

            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            //the ChartValues property will store our values array
            ChartValue = new ChartValues<MeasureModel>();
            ChartValue2 = new ChartValues<MeasureModel>();
            ChartValue3 = new ChartValues<MeasureModel>();
            ChartValue4 = new ChartValues<MeasureModel>();
            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex),
                    Values = ChartValue,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex1),
                    Values = ChartValue2,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex2),
                    Values = ChartValue3,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex3),
                    Values = ChartValue4,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                }
            };

            cartesianChart1.DisableAnimations = true;
            cartesianChart1.DataTooltip = null;
            cartesianChart1.Hoverable = false;
            cartesianChart1.LegendLocation = LegendLocation.Right;
            cartesianChart1.DefaultLegend.Visibility = Visibility.Visible;

            cartesianChart1.AxisX.Add(new Axis
            {
                DisableAnimations = true,
                LabelFormatter = value => new System.DateTime((long)value).ToString("mm:ss"),
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(1).Ticks
                }
            });

            var now = System.DateTime.Now;
            cartesianChart1.AxisX[0].MinValue = now.Ticks;
            SetAxisLimits(System.DateTime.Now);

            //The next code simulates data changes every 500 ms
            Timer = new Timer
            {
                Interval = 100
            };
            Timer.Tick += TimerOnTick3;
            Timer.Start();
        }

        //5
        public LiveChart(int RowIndex, int ColumnIndex, int RowIndex1, int ColumnIndex1, int RowIndex2, int ColumnIndex2, int RowIndex3, int ColumnIndex3, int RowIndex4, int ColumnIndex4)
        {
            this.RowIndex1 = RowIndex1;
            this.ColumnIndex1 = ColumnIndex1;
            this.ColumnIndex = ColumnIndex;
            this.RowIndex = RowIndex;
            this.RowIndex2 = RowIndex2;
            this.ColumnIndex2 = ColumnIndex2;
            this.ColumnIndex3 = ColumnIndex3;
            this.RowIndex3 = RowIndex3;
            this.RowIndex4 = RowIndex4;
            this.ColumnIndex4 = RowIndex4;

            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            //the ChartValues property will store our values array
            ChartValue = new ChartValues<MeasureModel>();
            ChartValue2 = new ChartValues<MeasureModel>();
            ChartValue3 = new ChartValues<MeasureModel>();
            ChartValue4 = new ChartValues<MeasureModel>();
            ChartValue5 = new ChartValues<MeasureModel>();
            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex),
                    Values = ChartValue,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex1),
                    Values = ChartValue2,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex2),
                    Values = ChartValue3,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex3),
                    Values = ChartValue4,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                },

                new LineSeries
                {
                    Title = String.Format("Index {0}", RowIndex4),
                    Values = ChartValue5,
                    PointGeometrySize = 0,
                    StrokeThickness = 1
                }
            };

            cartesianChart1.DisableAnimations = true;
            cartesianChart1.DataTooltip = null;
            cartesianChart1.Hoverable = false;
            cartesianChart1.LegendLocation = LegendLocation.Right;
            cartesianChart1.DefaultLegend.Visibility = Visibility.Visible;

            cartesianChart1.AxisX.Add(new Axis
            {
                DisableAnimations = true,
                LabelFormatter = value => new System.DateTime((long)value).ToString("mm:ss"),
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(1).Ticks
                }
            });

            var now = System.DateTime.Now;
            cartesianChart1.AxisX[0].MinValue = now.Ticks;
            SetAxisLimits(System.DateTime.Now);

            //The next code simulates data changes every 500 ms
            Timer = new Timer
            {
                Interval = 500
            };
            Timer.Tick += TimerOnTick4;
            Timer.Start();
        }


        //---------------------------------------------//
        public ChartValues<MeasureModel> ChartValue { get; set; }
        public ChartValues<MeasureModel> ChartValue2 { get; set; }
        public ChartValues<MeasureModel> ChartValue3 { get; set; }
        public ChartValues<MeasureModel> ChartValue4 { get; set; }
        public ChartValues<MeasureModel> ChartValue5 { get; set; }
        public Timer Timer { get; set; }

        private void SetAxisLimits(System.DateTime now)
        {
            cartesianChart1.AxisX[0].MaxValue = now.Ticks + TimeSpan.FromSeconds(1).Ticks; // lets force the axis to be 100ms ahead
        }

        public void TimerOnTick(object sender, EventArgs eventArgs)
        {
            var now = System.DateTime.Now;

            MeasureModel mm = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex, ColumnIndex)
            };

            ChartValue.Insert(index, mm);
            
            SetAxisLimits(now);
            index = index++;
        }

        public void TimerOnTick1(object sender, EventArgs eventArgs)
        {
            var now = System.DateTime.Now;

            MeasureModel mm = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex, ColumnIndex)
            };

            ChartValue.Insert(index, mm);

            MeasureModel mm1 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex1, RowIndex2)
            };

            ChartValue2.Insert(index, mm1);

            SetAxisLimits(now);
            index = index++;
        }

        public void TimerOnTick2(object sender, EventArgs eventArgs)
        {
            var now = System.DateTime.Now;

            MeasureModel mm = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex, ColumnIndex)
            };

            ChartValue.Insert(index, mm);

            MeasureModel mm1 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex1, RowIndex2)
            };

            ChartValue2.Insert(index, mm1);

            MeasureModel mm2 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex2, ColumnIndex2)
            };

            ChartValue3.Insert(index, mm2);

            SetAxisLimits(now);
            index = index++;
        }

        public void TimerOnTick3(object sender, EventArgs eventArgs)
        {
            var now = System.DateTime.Now;

            MeasureModel mm = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex, ColumnIndex)
            };

            ChartValue.Insert(index, mm);

            MeasureModel mm1 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex1, RowIndex2)
            };

            ChartValue2.Insert(index, mm1);

            MeasureModel mm2 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex2, ColumnIndex2)
            };

            ChartValue3.Insert(index, mm2);

            MeasureModel mm3 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex3, ColumnIndex3)
            };

            ChartValue4.Insert(index, mm3);

            SetAxisLimits(now);
            index = index++;
        }

        public void TimerOnTick4(object sender, EventArgs eventArgs)
        {
            var now = System.DateTime.Now;

            MeasureModel mm = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex, ColumnIndex)
            };

            ChartValue.Insert(index, mm);

            MeasureModel mm1 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex1, RowIndex2)
            };

            ChartValue2.Insert(index, mm1);

            MeasureModel mm2 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex2, ColumnIndex2)
            };

            ChartValue3.Insert(index, mm2);

            MeasureModel mm3 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex3, ColumnIndex3)
            };

            ChartValue4.Insert(index, mm3);

            MeasureModel mm4 = new MeasureModel
            {
                DateTime = now,
                Value = HomeVar.home.getValue(RowIndex4, ColumnIndex4)
            };

            ChartValue5.Insert(index, mm4);

            SetAxisLimits(now);
            index = index++;
        }


        public class MeasureModel
        {
            public System.DateTime DateTime { get; set; }
            public double Value { get; set; }
        }

        // Possible class for adding ranges instead of individually
    }

}

