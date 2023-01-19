using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Bonsai.Design.Visualizers;
using Font = System.Drawing.Font;
using FontStyle = System.Drawing.FontStyle;
using Size = System.Drawing.Size;

namespace PSTH
{
    public partial class HistogramListView : UserControl
    {
        private readonly SortedDictionary<UnitLabel, TimeSeriesGraph> _graphs =
            new SortedDictionary<UnitLabel, TimeSeriesGraph>();
        private readonly SortedDictionary<string, SortedArray<ushort>> _units =
            new SortedDictionary<string, SortedArray<ushort>>();
        private Label[] _legends;
        private string[] _electrodes;
        private SpikeHistogram _source;

        public HistogramListView()
        {
            InitializeComponent();
        }

        private static Label GetNewLegend(string @class, int index)
        {
            var label = new Label();
            label.Text = @class;
            label.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            label.Font = new Font("Arial", 12, FontStyle.Bold);
            label.ForeColor = GraphControl.GetColor(index);
            return label;
        }

        private static TimeSeriesGraph GetNewGraph(string title)
        {
            var graph = new TimeSeriesGraph();
            graph.Dock = DockStyle.Fill;
            graph.GraphPane.YAxis.Title.Text = "Firing Rate (Hz)";
            graph.GraphPane.Title.Text = title;
            graph.IsEnableWheelZoom = false;
            graph.IsEnableZoom = false;
            graph.Size = new Size(480, 270);
            return graph;
        }

        private void Reset()
        {
            _graphs.Clear();
            _units.Clear();
            _electrodes = null;
            _tableGraphs.Controls.Clear();
            _tableGraphs.RowCount = 1;
            _tableGraphs.ColumnCount = 1;
        }

        private void ResetLegend()
        {
            _legends = null;
            _tableLegend.Controls.Clear();
            _tableLegend.RowCount = 1;
            _tableLegend.ColumnCount = 1;
            _tableLegend.RowStyles.Clear();
        }

        public void UpdateTimeSeries(HistogramList histograms)
        {
            if (histograms == null || histograms.Count == 0)
            {
                Reset();
                ResetLegend();
                return;
            }

            _source = histograms.Source;

            var classMatch = _legends != null && histograms.ClassCount == _legends.Length &&
                             histograms.Classes.Zip(_legends, (@class, label) => @class.ToString() == label.Text)
                                 .All(b => b);
            if (!classMatch)
            {
                ResetLegend();
                _legends = histograms.Classes
                    .Select((@class, index) => GetNewLegend(@class.ToString(), index))
                    .ToArray();
                var legendCount = _legends.Length;

                for (var i = 0; i < legendCount; i++)
                {
                    _tableLegend.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
                    _tableLegend.Controls.Add(_legends[i], 0, i);
                }
            }

            var unitMatch = histograms.Count == _graphs.Count &&
                        histograms.Units.Zip(_graphs.Keys, (u1, u2) => u1 == u2).All(b => b);
            if (unitMatch)
            {
                foreach (var histogram in histograms)
                {
                    var graph = _graphs[histogram.Unit];
                    graph.UpdateTimeSeries(histogram.Data, histogram.BinEdges);
                }
            }
            else
            {
                Reset();
                _electrodes = histograms.Units.Select(u => u.Electrode).Distinct().ToArray();
                var columnCount = histograms.Units
                    .GroupBy(u => u.Electrode)
                    .Select(g => g.Count())
                    .Max();
                var rowCount = _electrodes.Length;
                _tableGraphs.RowCount = rowCount + 1;
                _tableGraphs.ColumnCount = columnCount + 1;
                _tableGraphs.RowStyles.Clear();
                _tableGraphs.ColumnStyles.Clear();

                for (var i = 0; i < rowCount; i++)
                {
                    //_tableGraphs.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rowCount));
                    _tableGraphs.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
                }
                _tableGraphs.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));

                for (var i = 0; i < columnCount; i++)
                {
                    //_tableGraphs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / rowCount));
                    _tableGraphs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 480));
                }
                _tableGraphs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));

                foreach (var electrode in _electrodes)
                {
                    _units.Add(electrode, new SortedArray<ushort>());
                }

                foreach (var histogram in histograms)
                {
                    var electrode = histogram.Unit.Electrode;
                    var row = Array.IndexOf(_electrodes, electrode);
                    _units[electrode].TryAdd(histogram.Unit.SortedId, out var column);
                    var graph = GetNewGraph(histogram.Unit.ToString());
                    if (_source != null)
                    {
                        graph.XMin = -_source.LeftHalfWindowMs;
                        graph.XMax = _source.RightHalfWindowMs;
                    }
                    graph.UpdateTimeSeries(histogram.Data, histogram.BinEdges);
                    _graphs[histogram.Unit] = graph;
                    _tableGraphs.Controls.Add(graph, column, row);
                }
            }
        }

        public void InvalidateTimeSeries()
        {
            foreach (var graph in _graphs.Values)
            {
                graph.Invalidate();
            }

            foreach (var legend in _legends)
            {
                legend.Invalidate();
            }
        }

        private void ButtonReset_Click(object sender, EventArgs e)
        {
            _source?.Reset();
        }
    }
}
