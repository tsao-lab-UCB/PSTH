using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bonsai.Design;
using Bonsai.Design.Visualizers;
using ZedGraph;

namespace PSTH
{
    public partial class TimeSeriesGraph : GraphControl
    {
        const float TitleFontSize = 14;
        const float YAxisMinSpace = 50;
        const float DefaultPaneMargin = 10;
        const float DefaultPaneTitleGap = 0.5f;

        bool _autoScaleX;
        bool _autoScaleY;
        SizeF _scaleFactor;
        int _channelCount;

        public TimeSeriesGraph()
        {
            _autoScaleX = true;
            _autoScaleY = true;
            IsShowContextMenu = false;
            GraphPane.Border.Color = Color.Red;
            GraphPane.Margin.Top = DefaultPaneMargin;
            GraphPane.Margin.Bottom = DefaultPaneMargin;
            GraphPane.Margin.Left = DefaultPaneMargin;
            GraphPane.Margin.Right = DefaultPaneMargin;
            GraphPane.Title.FontSpec.IsBold = true;
            GraphPane.Title.FontSpec.Size = TitleFontSize * _scaleFactor.Height;
            GraphPane.Title.Text = (0).ToString(CultureInfo.InvariantCulture);
            GraphPane.Title.IsVisible = true;
            GraphPane.TitleGap = DefaultPaneTitleGap;
            GraphPane.XAxis.Type = AxisType.Linear;
            GraphPane.XAxis.MinorTic.IsAllTics = false;
            GraphPane.XAxis.Title.IsVisible = true;
            GraphPane.XAxis.Title.Text = "Time (ms)";
            GraphPane.XAxis.IsVisible = true;
            GraphPane.YAxis.MinSpace = YAxisMinSpace;
            GraphPane.YAxis.IsVisible = true;
            GraphPane.YAxis.Title.IsVisible = true;
            GraphPane.Border.IsVisible = false;
            GraphPane.IsFontsScaled = true;
            GraphPane.AxisChangeEvent += GraphPane_AxisChangeEvent;
            ZoomEvent += graph_ZoomEvent;
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            _scaleFactor = factor;
            base.ScaleControl(factor, specified);
        }

        private void Reset(int channelCount)
        {
            ResetColorCycle();
            GraphPane.CurveList.Clear();
            for (var i = 0; i < channelCount; i++)
            {
                GraphPane.CurveList.Add(new LineItem(string.Empty, null, GetColor(i), SymbolType.None)
                    {
                        Line =
                        {
                            IsAntiAlias = true,
                            IsOptimizedDraw = true
                        },
                        Label =
                        {
                            IsVisible = false
                        }
                    }
                );
            }
        }

        public double XMin
        {
            get => GraphPane.XAxis.Scale.Min;
            set
            {
                foreach (var pane in MasterPane.PaneList)
                {
                    pane.XAxis.Scale.Min = value;
                }
                MasterPane.AxisChange();
                Invalidate();
            }
        }

        public double XMax
        {
            get => GraphPane.XAxis.Scale.Max;
            set
            {
                foreach (var pane in MasterPane.PaneList)
                {
                    pane.XAxis.Scale.Max = value;
                }
                MasterPane.AxisChange();
                Invalidate();
            }
        }

        public double YMin
        {
            get => GraphPane.YAxis.Scale.Min;
            set
            {
                foreach (var pane in MasterPane.PaneList)
                {
                    pane.YAxis.Scale.Min = value;
                }
                MasterPane.AxisChange();
                Invalidate();
            }
        }

        public double YMax
        {
            get => GraphPane.YAxis.Scale.Max;
            set
            {
                foreach (var pane in MasterPane.PaneList)
                {
                    pane.YAxis.Scale.Max = value;
                }
                MasterPane.AxisChange();
                Invalidate();
            }
        }

        public bool AutoScaleX
        {
            get => _autoScaleX;
            set
            {
                var changed = _autoScaleX != value;
                _autoScaleX = value;
                if (changed) OnAutoScaleXChanged(EventArgs.Empty);
            }
        }

        public bool AutoScaleY
        {
            get => _autoScaleY;
            set
            {
                var changed = _autoScaleY != value;
                _autoScaleY = value;
                if (changed) OnAutoScaleYChanged(EventArgs.Empty);
            }
        }

        internal int ChannelCount
        {
            get => _channelCount;
        }

        public event EventHandler AutoScaleXChanged;

        public event EventHandler AutoScaleYChanged;

        public event EventHandler AxisChanged;

        public void UpdateTimeSeries(double[,] samples, double[] ts)
        {
            if (samples == null || samples.GetLength(0) == 0 || samples.GetLength(1) == 0
                || ts == null || samples.GetLength(1) > ts.Length)
            {
                Reset(0);
                return;
            }

            var channelCount = samples.GetLength(0);
            var sampleCount = samples.GetLength(1);
            var channelCountChanged = _channelCount != channelCount;
            _channelCount = channelCount;
            if (GraphPane.CurveList.Count != channelCount || channelCountChanged)
            {
                Reset(channelCount);
            }

            var timeSeries = GraphPane.CurveList;
            for (var i = 0; i < channelCount; i++)
            {
                var points = new PointPairList();
                for (var j = 0; j < sampleCount; j++)
                {
                    points.Add(ts[j], samples[i, j]);
                }
                timeSeries[i].Points = points;
            }
        }

        private void graph_ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
        {
            MasterPane.AxisChange();
        }

        private void GraphPane_AxisChangeEvent(GraphPane pane)
        {
            AutoScaleX = pane.XAxis.Scale.MaxAuto;
            AutoScaleY = pane.YAxis.Scale.MaxAuto;
            AxisChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnAutoScaleXChanged(EventArgs e)
        {
            AutoScaleAxis = _autoScaleX || _autoScaleY;
            foreach (var pane in MasterPane.PaneList)
            {
                pane.XAxis.Scale.MaxAuto = _autoScaleX;
                pane.XAxis.Scale.MinAuto = _autoScaleX;
            }

            if (AutoScaleAxis) Invalidate();
            AutoScaleXChanged?.Invoke(this, e);
        }

        protected virtual void OnAutoScaleYChanged(EventArgs e)
        {
            AutoScaleAxis = _autoScaleX || _autoScaleY;
            foreach (var pane in MasterPane.PaneList)
            {
                pane.YAxis.Scale.MaxAuto = _autoScaleY;
                pane.YAxis.Scale.MinAuto = _autoScaleY;
            }

            if (AutoScaleAxis) Invalidate();
            AutoScaleYChanged?.Invoke(this, e);
        }
    }
}
