namespace PSTH
{
    partial class HistogramListView
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this._tableOutline = new System.Windows.Forms.TableLayoutPanel();
            this._tableGraphs = new System.Windows.Forms.TableLayoutPanel();
            this._tableLeftPanel = new System.Windows.Forms.TableLayoutPanel();
            this._tableLegend = new System.Windows.Forms.TableLayoutPanel();
            this._buttonReset = new System.Windows.Forms.Button();
            this._tableOutline.SuspendLayout();
            this._tableLeftPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // _tableOutline
            // 
            this._tableOutline.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._tableOutline.ColumnCount = 2;
            this._tableOutline.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this._tableOutline.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._tableOutline.Controls.Add(this._tableGraphs, 1, 0);
            this._tableOutline.Controls.Add(this._tableLeftPanel, 0, 0);
            this._tableOutline.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tableOutline.Location = new System.Drawing.Point(0, 0);
            this._tableOutline.Name = "_tableOutline";
            this._tableOutline.RowCount = 1;
            this._tableOutline.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._tableOutline.Size = new System.Drawing.Size(1600, 900);
            this._tableOutline.TabIndex = 0;
            // 
            // _tableGraphs
            // 
            this._tableGraphs.AutoScroll = true;
            this._tableGraphs.AutoScrollMargin = new System.Drawing.Size(10, 10);
            this._tableGraphs.AutoScrollMinSize = new System.Drawing.Size(10, 10);
            this._tableGraphs.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._tableGraphs.ColumnCount = 1;
            this._tableGraphs.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._tableGraphs.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._tableGraphs.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tableGraphs.Location = new System.Drawing.Point(203, 3);
            this._tableGraphs.Name = "_tableGraphs";
            this._tableGraphs.RowCount = 1;
            this._tableGraphs.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._tableGraphs.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._tableGraphs.Size = new System.Drawing.Size(1394, 894);
            this._tableGraphs.TabIndex = 0;
            // 
            // _tableLeftPanel
            // 
            this._tableLeftPanel.ColumnCount = 1;
            this._tableLeftPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._tableLeftPanel.Controls.Add(this._tableLegend, 0, 1);
            this._tableLeftPanel.Controls.Add(this._buttonReset, 0, 0);
            this._tableLeftPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tableLeftPanel.Location = new System.Drawing.Point(3, 3);
            this._tableLeftPanel.Name = "_tableLeftPanel";
            this._tableLeftPanel.RowCount = 2;
            this._tableLeftPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this._tableLeftPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._tableLeftPanel.Size = new System.Drawing.Size(194, 894);
            this._tableLeftPanel.TabIndex = 1;
            // 
            // _tableLegend
            // 
            this._tableLegend.AutoSize = true;
            this._tableLegend.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._tableLegend.ColumnCount = 1;
            this._tableLegend.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._tableLegend.Dock = System.Windows.Forms.DockStyle.Top;
            this._tableLegend.Location = new System.Drawing.Point(3, 43);
            this._tableLegend.Name = "_tableLegend";
            this._tableLegend.RowCount = 1;
            this._tableLegend.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this._tableLegend.Size = new System.Drawing.Size(188, 100);
            this._tableLegend.TabIndex = 2;
            // 
            // _buttonReset
            // 
            this._buttonReset.Dock = System.Windows.Forms.DockStyle.Fill;
            this._buttonReset.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._buttonReset.Location = new System.Drawing.Point(3, 3);
            this._buttonReset.Name = "_buttonReset";
            this._buttonReset.Size = new System.Drawing.Size(188, 34);
            this._buttonReset.TabIndex = 3;
            this._buttonReset.Text = "Reset";
            this._buttonReset.UseVisualStyleBackColor = true;
            this._buttonReset.Click += new System.EventHandler(this.ButtonReset_Click);
            // 
            // HistogramListView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._tableOutline);
            this.Name = "HistogramListView";
            this.Size = new System.Drawing.Size(1600, 900);
            this._tableOutline.ResumeLayout(false);
            this._tableLeftPanel.ResumeLayout(false);
            this._tableLeftPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel _tableOutline;
        private System.Windows.Forms.TableLayoutPanel _tableGraphs;
        private System.Windows.Forms.TableLayoutPanel _tableLeftPanel;
        private System.Windows.Forms.TableLayoutPanel _tableLegend;
        private System.Windows.Forms.Button _buttonReset;
    }
}
