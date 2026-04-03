using APITeams.Controllers;
using APITeams.Helpers;
using Microsoft.VisualBasic;
using OfficeOpenXml;
using OfficeOpenXml.Table;
using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace APITeams
{
    public partial class FrmInicioAlumno : Form
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        GraphExplorerClient graphExplorer = new GraphExplorerClient();
        GroupController groupController = new GroupController();
        ManageGroupController manageController = new ManageGroupController();
        CalendarController calendarController = new CalendarController();
        AppDatos clspase = new AppDatos();
        int idCurso = 0;
        private DataTable groups;
        private bool boolAgregarMiembros;
        private bool boolGenerarAgendas;
        private int opReporte;

        public FrmInicioAlumno(bool isAuto, bool inicio)
        {
            this.InitializeComponent();
            this.graphExplorer.InitApps();
            this.graphExplorer.GetAppClient();
            this.TraerDataSmart();
            if (ConstantsAPI.AUTOMATICO)
            {
                if (!isAuto)
                    return;
                this.FireAutomaticoAsync(inicio);
            }
            else
            {
                this.BtnCrearTeam.Enabled = false;
                this.BtnActualizarTeamsAl.Enabled = false;
            }
        }

        private async void FireAutomaticoAsync(bool inicio)
        {
            if (ConstantsAPI.CREARTEAMS)
            {
                if (inicio)
                    await groupController.VerificarTeamsAsync();
                int num = await this.AgregarTeams() ? 1 : 0;
                await this.groupController.UpdateActiveGroupsAsync();
            }
            if (ConstantsAPI.ACTUALIZARTEAMS)
            {
                int num1 = await this.ActualizarTeamsAsync(inicio) ? 1 : 0;
            }
            log.Debug("InicioEnvioLog");
            await this.groupController.EnvioLog();
            log.Debug("FinEnvioLog");
            FrmInicioAlumno.log.Info((object)"FIN DE APPTEAMS");
            Application.Exit();
            Environment.Exit(1);
        }

        private void TraerDataSmart()
        {
            this.groups = this.groupController.GetGroups();
            this.dataGridView1.DataSource = (object)this.groupController.GetGroups(true);
        }

        private async void BtnCrearTeam_Click(object sender, EventArgs e)
        {
            try
            {
                this.lblStatus.Text = "Ejecutando proceso";
                this.BtnCrearTeam.Enabled = false;
                this.tspbProgreso.Visible = true;
                this.tspbProgreso.Value = 50;
                if (!await this.AgregarTeams())
                    return;
                this.lblStatus.Text = "Proceso terminado: " + DateTime.Now.ToString();
                this.tspbProgreso.Value = 100;
                this.tspbProgreso.Visible = false;
                log.Debug("InicioEnvioLog");
                await this.groupController.EnvioLog();
                log.Debug("FinEnvioLog");
            }
            catch (Exception ex)
            {
                FrmInicioAlumno.log.Error((object)ex);
                log.Debug("InicioEnvioLog");
                await this.groupController.EnvioLog();
                log.Debug("FinEnvioLog");
                int num = (int)MessageBox.Show("Ha ocurrido un error");
                this.lblStatus.Text = "Proceso terminado con error: " + DateTime.Now.ToString();
                this.tspbProgreso.Value = 100;
                this.tspbProgreso.Visible = false;
            }
        }

        private async Task<bool> AgregarTeams()
        {
            FrmInicioAlumno.log.Debug((object)"Inicio de AgregarTeams");
            try
            {
                this.lblStatus.Text = "Ejecutando proceso";
                this.tspbProgreso.Visible = true;
                this.tspbProgreso.Maximum = this.groups.Rows.Count;
                int valueRow = 0;
                foreach (DataRow row in (InternalDataCollectionBase)this.groups.Rows)
                {
                    this.tspbProgreso.Value = valueRow;
                    this.idCurso = int.Parse(row["IdCurso"].ToString());
                    DataTable dataTable1 = new DataTable();
                    if (this.clspase.ConsultarDB(14, this.idCurso).Rows.Count == 0)
                    {
                        CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(this.idCurso);
                        this.lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();
                        this.tspbProgreso.Value = valueRow;
                        if (await creacion.CrearTeams())
                        {
                            this.boolAgregarMiembros = ConstantsAPI.ACTUALIZARTEAMS;
                            this.boolGenerarAgendas = ConstantsAPI.CREARAGENDAS;
                            DataTable dataTable2 = await creacion.ActualizarTeamsAsync(this.boolAgregarMiembros, this.boolGenerarAgendas);
                        }
                        creacion = (CreaciondeEquiposaDemanda)null;
                    }
                    ++valueRow;
                }
                FrmInicioAlumno.log.Info((object)"Fin de AgregarTeams");
                return true;
            }
            catch (Exception ex)
            {
                FrmInicioAlumno.log.Debug((object)"Fin de AgregarTeams con error");
                FrmInicioAlumno.log.Error((object)ex);
                return false;
            }
        }

        private async Task<bool> ActualizarTeamsAsync(bool inicio)
        {
            try
            {
                this.TraerDataSmart();
                this.tspbProgreso.Maximum = this.groups.Rows.Count;
                int valueRow = 0;
                foreach (DataRow row in (InternalDataCollectionBase)this.groups.Rows)
                {
                    this.tspbProgreso.Value = valueRow;
                    this.idCurso = int.Parse(row["IdCurso"].ToString());
                    DataTable dataTable1 = new DataTable();
                    if (this.clspase.ConsultarDB(14, this.idCurso).Rows.Count != 0)
                    {
                        CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(this.idCurso);
                        this.boolAgregarMiembros = ConstantsAPI.ACTUALIZARTEAMS;
                        this.boolGenerarAgendas = ConstantsAPI.CREARAGENDAS;
                        DataTable dataTable2 = await creacion.ActualizarTeamsAsync(this.boolAgregarMiembros, this.boolGenerarAgendas);
                        if (inicio)
                        {
                            DataTable dataTable3 = await creacion.ChequearMiembrosTeams();
                        }
                        creacion = (CreaciondeEquiposaDemanda)null;
                        creacion = (CreaciondeEquiposaDemanda)null;
                    }
                    ++valueRow;
                }
                return true;
            }
            catch (Exception ex)
            {
                FrmInicioAlumno.log.Error((object)this.idCurso);
                FrmInicioAlumno.log.Error((object)ex);
                return false;
            }
        }

        private void BtnCrearAgendas_Click(object sender, EventArgs e)
        {
        }

        private void BtnExportar_Click(object sender, EventArgs e)
        {
            switch (this.opReporte)
            {
                case 0:
                    this.ExportarDataGridViewExcel(this.dataGridView1);
                    break;
                case 1:
                    this.ExportarReporte();
                    break;
            }
        }

        private void ExportarReporte()
        {
            if (this.dataGridView1.DataSource == null)
                return;
            ExcelPackage.LicenseContext = new OfficeOpenXml.LicenseContext?(OfficeOpenXml.LicenseContext.NonCommercial);
            string str = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = "C:\\ReporteTeams\\";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            using (ExcelPackage excelPackage = new ExcelPackage(new FileInfo(path + "\\ReporteTeams" + str + ".xlsx")))
            {
                DataTable dataSource1 = (DataTable)this.dataGridView1.DataSource;
                DataTable dataSource2 = (DataTable)this.dataGridView2.DataSource;
                DataTable dataSource3 = (DataTable)this.dataGridView3.DataSource;
                DataTable table1 = dataSource1.DefaultView.ToTable();
                DataTable table2 = dataSource2.DefaultView.ToTable();
                DataTable table3 = dataSource3.DefaultView.ToTable();
                table1.TableName = "table000";
                table2.TableName = "table001";
                table3.TableName = "table002";
                ExcelWorksheet excelWorksheet1 = excelPackage.Workbook.Worksheets.Add("Cantidad Smart");
                excelWorksheet1.Cells["A1"].LoadFromDataTable(table1, true, TableStyles.Light1);
                excelWorksheet1.Cells[excelWorksheet1.Dimension.Address].AutoFitColumns();
                excelWorksheet1.Column(4).Style.Numberformat.Format = "yyyy-mm-dd";
                ExcelWorksheet excelWorksheet2 = excelPackage.Workbook.Worksheets.Add("DetalleSmart");
                excelWorksheet2.Cells["A1"].LoadFromDataTable(table2, true, TableStyles.Light1);
                excelWorksheet2.Cells[excelWorksheet2.Dimension.Address].AutoFitColumns();
                excelWorksheet2.Column(21).Style.Numberformat.Format = "yyyy-mm-dd";
                excelWorksheet2.Column(25).Style.Numberformat.Format = "yyyy-mm-dd";
                ExcelWorksheet excelWorksheet3 = excelPackage.Workbook.Worksheets.Add("DetalleTeams");
                excelWorksheet3.Cells["A1"].LoadFromDataTable(table3, true, TableStyles.Light1);
                excelWorksheet3.Cells[excelWorksheet3.Dimension.Address].AutoFitColumns();
                excelWorksheet3.Column(4).Style.Numberformat.Format = "yyyy-mm-dd";
                excelPackage.Save();
            }
            int num = (int)MessageBox.Show("Tu archivo esta listo en la carpeta " + path);
        }

        private void ExportarDataGridViewExcel(DataGridView grd)
        {
            if (grd.DataSource == null)
                return;
            ExcelPackage.LicenseContext = new OfficeOpenXml.LicenseContext?(OfficeOpenXml.LicenseContext.NonCommercial);
            string str = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = "C:\\ReporteTeams\\";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            using (ExcelPackage excelPackage = new ExcelPackage(new FileInfo(path + "\\Reporte" + str + ".xlsx")))
            {
                DataTable table = ((DataTable)grd.DataSource).DefaultView.ToTable();
                ExcelWorksheet excelWorksheet = excelPackage.Workbook.Worksheets.Add("Sheet1");
                excelWorksheet.Cells["A1"].LoadFromDataTable(table, true, TableStyles.Light1);
                excelWorksheet.Cells[excelWorksheet.Dimension.Address].AutoFitColumns();
                excelWorksheet.Column(4).Style.Numberformat.Format = "yyyy-mm-dd";
                excelPackage.Save();
            }
            int num = (int)MessageBox.Show("Tu archivo esta listo en la carpeta " + path);
        }

        private void FrmInicio_Load(object sender, EventArgs e) => this.lblStatus.Text = "Listo";

        private void EquiposInactivosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.dataGridView1.DataSource = (object)this.clspase.ConsultarDB(33);
            this.dataGridView1.AutoResizeColumns();
        }

        private void EquiposActivosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.dataGridView1.DataSource = (object)this.clspase.ConsultarDB(34);
            this.dataGridView1.AutoResizeColumns();
        }

        private void BtnBuscarSeccion_Click(object sender, EventArgs e)
        {
            if (this.txtCodigoSeccion.Text == null || this.txtCodigoSeccion.Text == "")
            {
                int num1 = (int)MessageBox.Show("El código a buscar no puede estar vacio");
            }
            else
            {
                this.gbSeccion.Visible = true;

                /*
                 APIMEETS configuracion de filtros
                 
                 */

                var arrUnidadNegocio = ConstantsAPI.UnidadNegocio.Split(',');
                var arrUnidadAcademica = ConstantsAPI.UnidadAcademica.Split(',');
                var arrPeriodo = ConstantsAPI.Periodo.Split(',');

                

                DataTable dataTable1 = new DataTable();
                DataTable dataTable2 = this.clspase.ConsultaTeams(23, this.txtCodigoSeccion.Text);
                if (dataTable2.Rows.Count != 0)
                {
                    var findUnidadNegocio = Array.IndexOf(arrUnidadNegocio, dataTable2.Rows[0][4].ToString());
                    var findUnidadAcademica = Array.IndexOf(arrUnidadNegocio, dataTable2.Rows[0][6].ToString());
                    var findPeriodo = Array.IndexOf(arrUnidadNegocio, dataTable2.Rows[0][9].ToString());

                    

                    this.lblSede.Text = dataTable2.Rows[0][1].ToString();
                    this.lblUnidadNegocio.Text = dataTable2.Rows[0][5].ToString();
                    this.lblUnidadAcademica.Text = dataTable2.Rows[0][7].ToString();
                    this.lblProducto.Text = dataTable2.Rows[0][11].ToString();
                    this.lblSemestre.Text = dataTable2.Rows[0][13].ToString();
                    this.lblCurso.Text = dataTable2.Rows[0][18].ToString();
                    this.lblcodigo.Text = dataTable2.Rows[0][20].ToString() + " - " + dataTable2.Rows[0][21].ToString();
                    this.toolTip1.SetToolTip((Control)this.lblProducto, dataTable2.Rows[0][11].ToString());
                    this.toolTip1.SetToolTip((Control)this.lblCurso, dataTable2.Rows[0][18].ToString());
                    this.idCurso = int.Parse(dataTable2.Rows[0][16].ToString());
                    DataTable dataTable3 = new DataTable();
                    DataTable dataTable4 = this.clspase.ConsultarDB(14, this.idCurso);
                    if (dataTable4.Rows.Count == 0)
                    {
                        this.btnProcesar.Enabled = false;
                        this.BtnNuevoDemanda.Enabled = true;
                        this.chkAgregarMiembros.Checked = false;
                        this.chkGenerarAgendas.Checked = false;
                        this.chkAgregarMiembros.Enabled = false;
                        this.chkGenerarAgendas.Enabled = false;
                        if (findPeriodo != -1 && findUnidadAcademica != -1 && findUnidadNegocio != -1)
                        {
                            btnAgendas.Visible = false;
                            btnAgendas.Enabled = false;

                        }
                    }
                    else
                    {
                        this.btnProcesar.Enabled = true;
                        this.BtnNuevoDemanda.Enabled = false;
                        this.chkAgregarMiembros.Checked = false;
                        this.chkGenerarAgendas.Checked = false;
                        this.chkAgregarMiembros.Enabled = true;
                        this.chkGenerarAgendas.Enabled = true;
                        this.dataGridView1.DataSource = (object)dataTable4;
                        if (findPeriodo == -1 && findUnidadAcademica == -1 && findUnidadNegocio == -1)
                        {
                            btnAgendas.Visible = true;
                            btnAgendas.Enabled = true;

                        }
                    }
                }
                else
                {
                    this.gbSeccion.Visible = false;
                    int num2 = (int)MessageBox.Show("No se encontro el horario solicitado");
                }
            }
        }

        private async void BtnProcesar_ClickAsync(object sender, EventArgs e)
        {
            if (this.idCurso == 0)
            {
                int num1 = (int)MessageBox.Show("Error");
            }
            else
            {
                Cursor.Current = Cursors.WaitCursor;
                ToolStripStatusLabel lblStatus1 = this.lblStatus;
                DateTime now = DateTime.Now;
                string str1 = "Procesando desde: " + now.ToString();
                lblStatus1.Text = str1;
                this.tspbProgreso.Visible = true;
                this.btnProcesar.Enabled = false;
                this.boolGenerarAgendas = ConstantsAPI.CREARAGENDAS;
                CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(this.idCurso);
                DataTable dataTable = await creacion.ActualizarTeamsAsync(this.boolAgregarMiembros, this.boolGenerarAgendas);
                this.dataGridView1.DataSource = (object)await creacion.ChequearMiembrosTeams();
                this.tspbProgreso.Visible = false;
                int num2 = (int)MessageBox.Show("Actualizado correctamente, espere 10 minutos para que se refleje en MSTeams");
                ToolStripStatusLabel lblStatus2 = this.lblStatus;
                now = DateTime.Now;
                string str2 = "Proceso Terminado: " + now.ToString();
                lblStatus2.Text = str2;
                this.btnProcesar.Enabled = true;
                Cursor.Current = Cursors.Default;
                creacion = (CreaciondeEquiposaDemanda)null;
            }
        }

        private async void BtnNuevoDemanda_Click(object sender, EventArgs e)
        {
            if (this.idCurso == 0)
            {
                int num1 = (int)MessageBox.Show("Error: Revisa el codigo de horario");
            }
            else
            {
                Cursor.Current = Cursors.WaitCursor;
                CreaciondeEquiposaDemanda creaciondeEquiposaDemanda = new CreaciondeEquiposaDemanda(this.idCurso);
                this.BtnNuevoDemanda.Enabled = false;
                this.tspbProgreso.Visible = true;
                this.lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();
                this.tspbProgreso.Maximum = 100;
                if (!await creaciondeEquiposaDemanda.CrearTeams())
                    return;
                this.tspbProgreso.Value = 50;
                int num2 = (int)MessageBox.Show("Recuerda que debes activar el equipo creado antes de agregar miembros y enviar agendas");
                this.btnProcesar.Enabled = true;
                this.btnBuscarSeccion.Enabled = true;
                this.lblStatus.Text = "Proceso terminado: " + DateTime.Now.ToString();
                this.tspbProgreso.Value = 100;
                Cursor.Current = Cursors.Default;
                this.BtnNuevoDemanda.Enabled = true;
                this.tspbProgreso.Visible = false;
                int num3 = (int)MessageBox.Show("Proceso terminado");
            }
        }

        private async void BtnBorrarAgendas_ClickAsync(object sender, EventArgs e)
        {
            this.idCurso = 0;
            DataTable dataTable = await new CreaciondeEquiposaDemanda(this.idCurso).DeleteEvents();
        }

        private void ChkAgregarMiembros_CheckedChanged(object sender, EventArgs e)
        {
            this.boolAgregarMiembros = this.chkAgregarMiembros.Checked;
        }

        private void ChkGenerarAgendas_CheckedChanged(object sender, EventArgs e)
        {
            this.boolGenerarAgendas = this.chkGenerarAgendas.Checked;
        }

        private void ReporteDeHorariosToolStripMenuItem_ClickAsync(object sender, EventArgs e)
        {
            this.dataGridView1.DataSource = (object)new CreaciondeEquiposaDemanda(this.idCurso).ReporteHorarios(int.Parse(Interaction.InputBox("Escribe el numero de días que deseas el reporte a partir de hoy", "Número de Días", "1", 100, 0)));
        }

        private async void BtnReporteMiembros_ClickAsync(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            this.lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();
            CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(this.idCurso);
            DataTable dt = creacion.ReporteMiembros();
            dt.Columns.Add("TotalTeams", typeof(Int32));
            DataSet result = new DataSet();
            this.tspbProgreso.Maximum = dt.Rows.Count;
            int valueRow = 0;
            foreach (DataRow row in (InternalDataCollectionBase)dt.Rows)
            {
                this.idCurso = int.Parse(row[18].ToString());
                try
                {
                    row[19] = await creacion.ObtenerTotalTeams(row[16].ToString());
                    result = await creacion.ReporteMiembrosDetalleAsync(this.idCurso);
                }
                catch (Exception ex)
                {
                    FrmInicioAlumno.log.Error((object)ex);
                }
                ++valueRow;
            }
            this.dataGridView1.DataSource = (object)dt;
            this.dataGridView1.AutoResizeColumns();
            this.dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView2.DataSource = (object)result.Tables[1];
            this.dataGridView2.AutoResizeColumns();
            this.dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView3.DataSource = (object)result.Tables[0];
            this.dataGridView3.AutoResizeColumns();
            this.dataGridView3.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.opReporte = 1;
            Cursor.Current = Cursors.Default;
            int num = (int)MessageBox.Show("Proceso terminado");
            creacion = (CreaciondeEquiposaDemanda)null;
            dt = (DataTable)null;
            result = (DataSet)null;
        }

        private async void ReporteDeMiembrosToolStripMenuItem_ClickAsync(object sender, EventArgs e)
        {
            CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(0);
            DataTable dt = creacion.ReporteMiembros();
            DataTable detalleSmart = new DataTable();
            DataTable detalleTeams = new DataTable();
            foreach (DataRow row in (InternalDataCollectionBase)dt.Rows)
            {
                this.idCurso = int.Parse(row[18].ToString());
                try
                {
                    DataSet dataSet = await creacion.ReporteMiembrosDetalleAsync(this.idCurso);
                    detalleSmart.Merge(dataSet.Tables[1]);
                    detalleTeams.Merge(dataSet.Tables[0]);
                }
                catch (Exception ex)
                {
                    FrmInicioAlumno.log.Error((object)ex);
                }
            }
            this.dataGridView1.DataSource = (object)dt;
            this.dataGridView1.AutoResizeColumns();
            this.dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView2.DataSource = (object)detalleSmart;
            this.dataGridView2.AutoResizeColumns();
            this.dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView3.DataSource = (object)detalleTeams;
            this.dataGridView3.AutoResizeColumns();
            this.dataGridView3.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            this.opReporte = 1;
            creacion = (CreaciondeEquiposaDemanda)null;
            dt = (DataTable)null;
            detalleSmart = (DataTable)null;
            detalleTeams = (DataTable)null;
        }

        private void BuscarPorAlumno_Click(object sender, EventArgs e)
        {
            if (this.txtCodigoAlumno.Text == null || this.txtCodigoAlumno.Text == "")
            {
                int num1 = (int)MessageBox.Show("El código a buscar no puede estar vacio");
            }
            else
            {
                this.gbAlumno.Visible = true;
                DataTable dataTable1 = new DataTable();
                DataTable dataTable2 = this.clspase.ConsultaTeams(35, this.txtCodigoAlumno.Text);
                this.dataGridView1.DataSource = (object)dataTable2;
                if (dataTable2.Rows.Count != 0)
                {
                    this.lblAlSede.Text = dataTable2.Rows[0][1].ToString();
                    this.lblAlDivision.Text = dataTable2.Rows[0][2].ToString();
                    this.lblAlPrograma.Text = dataTable2.Rows[0][3].ToString();
                    this.lblAlProducto.Text = dataTable2.Rows[0][5].ToString();
                    this.lblAlSemestre.Text = dataTable2.Rows[0][4].ToString();
                    this.lblAlCurso.Text = dataTable2.Rows[0][7].ToString();
                    this.lblAlAlumno.Text = dataTable2.Rows[0][19].ToString() + " - " + dataTable2.Rows[0][18].ToString();
                    this.toolTip1.SetToolTip((Control)this.lblProducto, dataTable2.Rows[0][11].ToString());
                    this.toolTip1.SetToolTip((Control)this.lblCurso, dataTable2.Rows[0][18].ToString());
                    this.idCurso = int.Parse(dataTable2.Rows[0][0].ToString());
                    DataTable dataTable3 = new DataTable();
                    if (this.clspase.ConsultarDB(14, this.idCurso).Rows.Count == 0)
                    {
                        this.btnProcesar.Enabled = false;
                        this.BtnNuevoDemanda.Enabled = true;
                        this.chkAgregarMiembros.Checked = false;
                        this.chkGenerarAgendas.Checked = false;
                        this.chkAgregarMiembros.Enabled = false;
                        this.chkGenerarAgendas.Enabled = false;
                    }
                    else
                    {
                        this.btnProcesar.Enabled = true;
                        this.BtnNuevoDemanda.Enabled = false;
                        this.chkAgregarMiembros.Checked = false;
                        this.chkGenerarAgendas.Checked = false;
                        this.chkAgregarMiembros.Enabled = true;
                        this.chkGenerarAgendas.Enabled = true;
                    }
                }
                else
                {
                    this.gbAlumno.Visible = false;
                    int num2 = (int)MessageBox.Show("No se encontro el alumno solicitado");
                }
            }
        }

        private async void BtnActualizarTeamsAl_ClickAsync(object sender, EventArgs e)
        {
            try
            {
                this.groups = this.clspase.ConsultaTeams(35, this.txtCodigoAlumno.Text.Trim());
                this.tspbProgreso.Maximum = this.groups.Rows.Count;
                int valueRow = 0;
                Cursor.Current = Cursors.WaitCursor;
                this.lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();
                this.tspbProgreso.Visible = true;
                this.BtnActualizarTeamsAl.Enabled = false;
                foreach (DataRow row in (InternalDataCollectionBase)this.groups.Rows)
                {
                    this.tspbProgreso.Value = valueRow;
                    this.idCurso = int.Parse(row["IdCurso"].ToString());
                    DataTable dataTable1 = new DataTable();
                    if (this.clspase.ConsultarDB(14, this.idCurso).Rows.Count != 0)
                    {
                        this.tspbProgreso.Value = valueRow;
                        CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(this.idCurso);
                        this.boolAgregarMiembros = ConstantsAPI.ACTUALIZARTEAMS;
                        this.boolGenerarAgendas = ConstantsAPI.CREARAGENDAS;
                        if (row[13].ToString() != "-SIN FACILITADOR-")
                        {
                            DataTable dataTable2 = await creacion.ActualizarTeamsAsync(this.boolAgregarMiembros, this.boolGenerarAgendas);
                            DataTable dataTable3 = await creacion.ChequearMiembrosTeams();
                        }
                        creacion = (CreaciondeEquiposaDemanda)null;
                    }
                    ++valueRow;
                }
                this.tspbProgreso.Visible = false;
                int num = (int)MessageBox.Show("Actualizado correctamente, espere 10 minutos para que se refleje en MSTeams");
                this.lblStatus.Text = "Proceso Terminado: " + DateTime.Now.ToString();
                this.BtnActualizarTeamsAl.Enabled = true;
                this.groups = this.clspase.ConsultaTeams(35, this.txtCodigoAlumno.Text.Trim());
                this.dataGridView1.DataSource = (object)this.groups;
            }
            catch (Exception ex)
            {
                FrmInicioAlumno.log.Error((object)ex);
            }
        }

        private void reporteDeAvanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.dataGridView1.DataSource = (object)this.clspase.ConsultarDB(36);
            this.dataGridView1.AutoResizeColumns();
        }

        private async void BtnVerificarTeams_Click(object sender, EventArgs e)
        {
            if (this.txtCodigoSeccion.Text == null || this.txtCodigoSeccion.Text == "")
            {
                int num1 = (int)MessageBox.Show("El código a buscar no puede estar vacio");
            }
            else
            {
                this.gbSeccion.Visible = true;
                DataTable dataTable1 = new DataTable();
                DataTable dataTable2 = this.clspase.ConsultaTeams(23, this.txtCodigoSeccion.Text);
                if (dataTable2.Rows.Count != 0)
                {
                    this.lblSede.Text = dataTable2.Rows[0][1].ToString();
                    this.lblUnidadNegocio.Text = dataTable2.Rows[0][5].ToString();
                    this.lblUnidadAcademica.Text = dataTable2.Rows[0][7].ToString();
                    this.lblProducto.Text = dataTable2.Rows[0][11].ToString();
                    this.lblSemestre.Text = dataTable2.Rows[0][13].ToString();
                    this.lblCurso.Text = dataTable2.Rows[0][18].ToString();
                    this.lblcodigo.Text = dataTable2.Rows[0][20].ToString() + " - " + dataTable2.Rows[0][21].ToString();
                    this.toolTip1.SetToolTip((Control)this.lblProducto, dataTable2.Rows[0][11].ToString());
                    this.toolTip1.SetToolTip((Control)this.lblCurso, dataTable2.Rows[0][18].ToString());
                    this.idCurso = int.Parse(dataTable2.Rows[0][16].ToString());
                    if (await this.groupController.VerificarTeamsAsync(true, this.idCurso))
                    {
                        DataTable dataTable3 = new DataTable();
                        DataTable dataTable4 = this.clspase.ConsultarDB(14, this.idCurso);
                        if (dataTable4.Rows.Count == 0)
                        {
                            this.btnProcesar.Enabled = false;
                            this.BtnNuevoDemanda.Enabled = true;
                            this.chkAgregarMiembros.Checked = false;
                            this.chkGenerarAgendas.Checked = false;
                            this.chkAgregarMiembros.Enabled = false;
                            this.chkGenerarAgendas.Enabled = false;
                        }
                        else
                        {
                            this.btnProcesar.Enabled = true;
                            this.BtnNuevoDemanda.Enabled = false;
                            this.chkAgregarMiembros.Checked = false;
                            this.chkGenerarAgendas.Checked = false;
                            this.chkAgregarMiembros.Enabled = true;
                            this.chkGenerarAgendas.Enabled = true;
                            this.dataGridView1.DataSource = (object)dataTable4;
                        }
                    }
                    else
                    {
                        DataTable dataTable5 = new DataTable();
                        DataTable dataTable6 = this.clspase.ConsultarDB(14, this.idCurso);
                        if (dataTable6.Rows.Count == 0)
                        {
                            this.btnProcesar.Enabled = false;
                            this.BtnNuevoDemanda.Enabled = true;
                            this.chkAgregarMiembros.Checked = false;
                            this.chkGenerarAgendas.Checked = false;
                            this.chkAgregarMiembros.Enabled = false;
                            this.chkGenerarAgendas.Enabled = false;
                            int num2 = (int)MessageBox.Show("El equipo no esta en MSTeams por favor vuelve a crearlo haciendo click en el boton NUEVO");
                        }
                        else
                        {
                            this.btnProcesar.Enabled = true;
                            this.BtnNuevoDemanda.Enabled = false;
                            this.chkAgregarMiembros.Checked = false;
                            this.chkGenerarAgendas.Checked = false;
                            this.chkAgregarMiembros.Enabled = true;
                            this.chkGenerarAgendas.Enabled = true;
                            this.dataGridView1.DataSource = (object)dataTable6;
                        }
                    }
                }
                else
                {
                    this.gbSeccion.Visible = false;
                    int num3 = (int)MessageBox.Show("No se encontro el horario solicitado");
                }
            }
        }

        private void borrarEquipoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.txtCodigoSeccion.Text == null || this.txtCodigoSeccion.Text == "")
            {
                int num = (int)MessageBox.Show("El código a buscar no puede estar vacio");
            }
            else
            {
                if (!(Interaction.InputBox("Escriba la contraseña para el borrado del equipo " + this.txtCodigoSeccion.Text, "BORRAR EQUIPO", XPos: 100, YPos: 0) == ConstantsAPI.PASS_APP))
                    return;
                this.clspase.DeleteCodigoSeccion(41, this.txtCodigoSeccion.Text);
            }
        }



        private void depuraciónEquiposToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.txtCodigoSeccion.Text == null || this.txtCodigoSeccion.Text == "")
            {
                int num = (int)MessageBox.Show("El código a buscar no puede estar vacio");
            }
            else
            {
                if (!(Interaction.InputBox("Escriba la contraseña para el borrado del equipo " + this.txtCodigoSeccion.Text, "BORRAR EQUIPO", XPos: 100, YPos: 0) == ConstantsAPI.PASS_APP))
                    return;
                this.clspase.DeleteCodigoSeccion(41, this.txtCodigoSeccion.Text);
            }

            //try
            //{
            //    DataTable ds = new DataTable();
            //    ds = clspase.ConsultarDB(42);
            //    int conteo = 0;
            //    if (ds.Rows.Count != 0)
            //    {
            //        foreach (DataRow row in ds.Rows)
            //        {
            //            idCurso = int.Parse(row["IdSeccionSmart"].ToString());
            //            string IdTeamsGroup = row["IdTeamsGroup"].ToString();
            //            CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(this.idCurso);
            //            if(await creacion.VerificarTeamsBorrar(IdTeamsGroup))
            //                conteo++;
            //        }
            //        log.Debug(conteo);

            //    }
            //}
            //catch (Exception)
            //{

            //    throw;
            //}
        }

        private void btnAgendas_Click(object sender, EventArgs e)
        {

        }
    }
}
