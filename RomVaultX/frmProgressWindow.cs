using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace RomVaultX
{
    public partial class FrmProgressWindow : Form
    {
        private readonly string _titleRoot;
        private readonly Form _parentForm;
        private bool _errorOpen;
        private bool _bDone;

        public FrmProgressWindow(Form parentForm, string titleRoot, DoWorkEventHandler function)
        {
            _parentForm = parentForm;
            _titleRoot = titleRoot;
            InitializeComponent();

            ClientSize = new Size(498, 131);

            _titleRoot = titleRoot;

            bgWork.DoWork += function;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CP_NOCLOSE_BUTTON = 0x200;
                CreateParams mdiCp = base.CreateParams;
                mdiCp.ClassStyle |= CP_NOCLOSE_BUTTON;
                return mdiCp;
            }
        }


        private void FrmProgressWindowNewShown(object? sender, EventArgs e)
        {
            bgWork.ProgressChanged += BgwProgressChanged;
            bgWork.RunWorkerCompleted += BgwRunWorkerCompleted;
            bgWork.RunWorkerAsync(SynchronizationContext.Current);
        }

        private void BgwProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            if (e.UserState == null)
            {
                if ((e.ProgressPercentage >= progressBar.Minimum) && (e.ProgressPercentage <= progressBar.Maximum))
                {
                    progressBar.Value = e.ProgressPercentage;
                }
                UpdateStatusText();
                return;
            }

            if (e.UserState is BgwText bgwT)
            {
                label.Text = bgwT.Text;
                return;
            }
            if (e.UserState is BgwSetRange bgwSR)
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = bgwSR.MaxVal >= 0 ? bgwSR.MaxVal : 0;
                progressBar.Value = 0;
                UpdateStatusText();
                return;
            }


            if (e.UserState is BgwText2 bgwT2)
            {
                label2.Text = bgwT2.Text;
                return;
            }

            if (e.UserState is BgwValue2 bgwV2)
            {
                if ((bgwV2.Value >= progressBar2.Minimum) && (bgwV2.Value <= progressBar2.Maximum))
                {
                    progressBar2.Value = bgwV2.Value;
                }
                UpdateStatusText2();
                return;
            }

            if (e.UserState is BgwSetRange2 bgwSR2)
            {
                progressBar2.Minimum = 0;
                progressBar2.Maximum = bgwSR2.MaxVal >= 0 ? bgwSR2.MaxVal : 0;
                progressBar2.Value = 0;
                UpdateStatusText2();
                return;
            }
            if (e.UserState is BgwRange2Visible bgwR2V)
            {
                label2.Visible = bgwR2V.Visible;
                progressBar2.Visible = bgwR2V.Visible;
                lbl2Prog.Visible = bgwR2V.Visible;
                return;
            }


            if (e.UserState is BgwText3 bgwT3)
            {
                label3.Text = bgwT3.Text;
                return;
            }


            if (e.UserState is BgwShowError bgwSDE)
            {
                if (!_errorOpen)
                {
                    _errorOpen = true;
                    ClientSize = new Size(498, 292);
                    MinimumSize = new Size(498, 292);
                    FormBorderStyle = FormBorderStyle.SizableToolWindow;
                }

                ErrorGrid.Rows.Add();
                int row = ErrorGrid.Rows.Count - 1;

                ErrorGrid.Rows[row].Cells["CError"].Value = bgwSDE.error;

                ErrorGrid.Rows[row].Cells["CErrorFile"].Value = bgwSDE.filename;

                if (row >= 0)
                {
                    ErrorGrid.FirstDisplayedScrollingRowIndex = row;
                }
            }
        }

        private void UpdateStatusText()
        {
            int range = progressBar.Maximum - progressBar.Minimum;
            int percent = range > 0 ? progressBar.Value * 100 / range : 0;

            Text = _titleRoot + $" - {percent}% complete";
        }

        private void UpdateStatusText2()
        {
            lbl2Prog.Text = progressBar2.Maximum > 0 ? $"{progressBar2.Value}/{progressBar2.Maximum}" : "";
        }

        private void BgwRunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            if (_errorOpen)
            {
                cancelButton.Text = "Close";
                cancelButton.Enabled = true;
                _bDone = true;
            }
            else
            {
                _parentForm.Show();
                Close();
            }
        }

        private void CancelButtonClick(object? sender, EventArgs e)
        {
            if (_bDone)
            {
                if (!_parentForm.Visible)
                {
                    _parentForm.Show();
                }
                Close();
            }
            else
            {
                cancelButton.Text = "Cancelling";
                cancelButton.Enabled = false;
                bgWork.CancelAsync();
            }
        }

        private void ErrorGridSelectionChanged(object? sender, EventArgs e)
        {
            ErrorGrid.ClearSelection();
        }

        private void FrmProgressWindow_Resize(object? sender, EventArgs e)
        {
            switch (WindowState)
            {
                case FormWindowState.Minimized:
                    if (_parentForm.Visible)
                    {
                        _parentForm.Hide();
                    }
                    return;
                case FormWindowState.Maximized:
                    if (!_parentForm.Visible)
                    {
                        _parentForm.Show();
                    }
                    return;
                case FormWindowState.Normal:
                    if (!_parentForm.Visible)
                    {
                        _parentForm.Show();
                    }
                    return;
            }
        }

        private void Complete(object? sender, RunWorkerCompletedEventArgs e)
        {
            Exception? er = e.Error;
            if (er != null)
            {
                MessageBox.Show(e.Error!.ToString(), "RomVault", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
