using APITeams.Controllers;
using APITeams.Helpers;
using OfficeOpenXml;
using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace APITeams
{
    public partial class frmInicio : Form
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        GraphExplorerClient graphExplorer = new GraphExplorerClient();
        GroupController groupController = new GroupController();
        ManageGroupController manageController = new ManageGroupController();
        CalendarController calendarController = new CalendarController();
        AppDatos clspase = new AppDatos();
        int idCurso = 0;
        public void FrmInicio(bool isAuto)
        {
            InitializeComponent();
            graphExplorer.InitApps();
            graphExplorer.GetAppClient();
            TraerDataSmart();
            if (ConstantsAPI.AUTOMATICO)
            {
                if (isAuto)
                {
                    FireAutomaticoAsync();
                }
            }
            else
            {
                btnCrearTeam.Enabled = false;
                btnActualizarTeams.Enabled = false;
                btnCrearAgendas.Enabled = false;
            }


        }

        private async void FireAutomaticoAsync()
        {
            if (ConstantsAPI.CREARTEAMS)
            {
                await AgregarTeams();
            }
            if (ConstantsAPI.ACTUALIZARTEAMS)
            {
                await ActualizarTeams();
            }
            Application.Exit();
        }

        DataTable groups = null;
        private void TraerDataSmart()
        {
            groups = groupController.GetGroups();
            dataGridView1.DataSource = groups;
            //await groupController.UpdateActiveGroupsAsync();
        }

        private async void BtnCrearTeam_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Ejecutando proceso";
                //PRIMERA CORRIDA
                btnCrearTeam.Enabled = false;
                tspbProgreso.Visible = true;
                tspbProgreso.Value = 50;
                if (await AgregarTeams())
                {
                    //DataTable groups = await groupController.CreateGroups();
                    //dataGridView1.DataSource = groups;
                    lblStatus.Text = "Proceso terminado: " + DateTime.Now.ToString();
                    btnCrearTeam.Enabled = true;
                    tspbProgreso.Value = 100;
                    tspbProgreso.Visible = false;
                }
            }
            catch (Exception err)
            {
                log.Error(err);
                MessageBox.Show("Ha ocurrido un error");
                lblStatus.Text = "Proceso terminado con error: " + DateTime.Now.ToString();
                tspbProgreso.Value = 100;
                tspbProgreso.Visible = false;
            }
        }

        private async Task<bool> AgregarTeams()
        {
            log.Debug("Inicio de AgregarTeams");
            try
            {
                lblStatus.Text = "Ejecutando proceso";
                tspbProgreso.Visible = true;
                tspbProgreso.Maximum = groups.Rows.Count;
                int valueRow = 0;
                foreach (DataRow row in groups.Rows)
                {
                    tspbProgreso.Value = valueRow;
                    idCurso = int.Parse(row["IdCurso"].ToString());
                    DataTable ds = new DataTable();
                    ds = clspase.ConsultarDB(14, idCurso);
                    if (ds.Rows.Count == 0)
                    {
                        CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
                        lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();
                        tspbProgreso.Value = 50;
                        if (await creacion.CrearTeams())
                        {
                            boolAgregarMiembros = ConstantsAPI.ACTUALIZARTEAMS;
                            boolGenerarAgendas = ConstantsAPI.CREARAGENDAS;
                            DataTable TeamsActualizar = await creacion.ActualizarTeamsAsync(boolAgregarMiembros, boolGenerarAgendas);
                        }
                    }
                    valueRow++;
                }
                log.Debug("Fin de AgregarTeams");
                return true;
            }
            catch (Exception error)
            {
                log.Debug("Fin de AgregarTeams con error");
                log.Error(error);
                return false;
            }

        }

        private async void BtnActualizarTeams_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Ejecutando proceso";
                tspbProgreso.Visible = true;
                btnActualizarTeams.Enabled = false;
                // await groupController.UpdateActiveGroupsAsync();
                if (await ActualizarTeams())
                {
                    //Actualizamos el estado activo de los teams
                    //await groupController.UpdateActiveGroups();
                    //DataTable groups = await manageController.UpdateGroups();
                    dataGridView1.DataSource = groups;
                    lblStatus.Text = "Proceso terminado: " + DateTime.Now.ToString();
                    btnActualizarTeams.Enabled = true;
                    tspbProgreso.Value = 100;
                    tspbProgreso.Visible = false;
                }
            }
            catch (Exception err)
            {
                log.Error(err);
                MessageBox.Show("Ha ocurrido un error");
                lblStatus.Text = "Proceso terminado con error: " + DateTime.Now.ToString();
                tspbProgreso.Value = 100;
                tspbProgreso.Visible = false;
            }
        }

        private async Task<bool> ActualizarTeams()
        {
            try
            {
                TraerDataSmart();

                tspbProgreso.Maximum = groups.Rows.Count;
                int valueRow = 0;
                foreach (DataRow row in groups.Rows)
                {
                    tspbProgreso.Value = valueRow;
                    //idCurso = int.Parse(row.Cells["IdCurso"].Value.ToString());
                    idCurso = int.Parse(row["IdCurso"].ToString());
                    DataTable ds = new DataTable();
                    ds = clspase.ConsultarDB(14, idCurso);
                    if (ds.Rows.Count != 0)
                    {
                        CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
                        boolAgregarMiembros = ConstantsAPI.ACTUALIZARTEAMS;
                        boolGenerarAgendas = ConstantsAPI.CREARAGENDAS;
                        DataTable TeamsActualizar = await creacion.ActualizarTeamsAsync(boolAgregarMiembros, boolGenerarAgendas);
                    }
                    valueRow++;
                }
                return true;
            }
            catch (Exception error)
            {
                log.Error(error);
                return false;
            }

        }

        private void BtnCrearAgendas_Click(object sender, EventArgs e)
        {
            //try
            //{
            //    lblStatus.Text = "Ejecutando proceso";
            //    //SEGUNDA CORRIDA
            //    btnCrearAgendas.Enabled = false;
            //    tspbProgreso.Visible = true;
            //    tspbProgreso.Value = 50;

            //    DataTable groups = await calendarController.CreateEvents();
            //    await calendarController.UpdateEvents();
            //    dataGridView1.DataSource = groups;
            //    btnCrearAgendas.Enabled = true;
            //    lblStatus.Text = "Proceso terminado: " + DateTime.Now.ToString();
            //    tspbProgreso.Value = 100;
            //    tspbProgreso.Visible = false;

            //}
            //catch (Exception err)
            //{

            //    btnCrearAgendas.Enabled = true;
            //    MessageBox.Show("Ha ocurrido un error");
            //    lblStatus.Text = "Proceso terminado con error: " + DateTime.Now.ToString();
            //    tspbProgreso.Value = 100;
            //    tspbProgreso.Visible = false;
            //    log.Error(err);
            //}
        }

        private void BtnExportar_Click(object sender, EventArgs e)
        {
            switch (opReporte)
            {
                case 0:
                    ExportarDataGridViewExcel(dataGridView1);
                    break;
                case 1:
                    ExportarReporte();
                    break;
                default:
                    break;
            }


        }

        private void ExportarReporte()
        {
            if (dataGridView1.DataSource != null)
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                var dt = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string folderLocation = "C:\\ReporteTeams\\";
                bool exists = System.IO.Directory.Exists(folderLocation);

                if (!exists)
                    System.IO.Directory.CreateDirectory(folderLocation);


                System.IO.FileInfo file = new System.IO.FileInfo(folderLocation + "\\ReporteTeams" + dt + ".xlsx");
                using (ExcelPackage pck = new ExcelPackage(file))
                {
                    //BindingSource bs = (BindingSource)grd.DataSource;
                    DataTable table = (DataTable)dataGridView1.DataSource;
                    DataTable table1 = (DataTable)dataGridView2.DataSource;
                    DataTable table2 = (DataTable)dataGridView3.DataSource;
                    System.Data.DataTable filtered = table.DefaultView.ToTable();
                    System.Data.DataTable filtered1 = table1.DefaultView.ToTable();
                    System.Data.DataTable filtered2 = table2.DefaultView.ToTable();
                    filtered.TableName = "table000";
                    filtered1.TableName = "table001";
                    filtered2.TableName = "table002";
                    ExcelWorksheet ws = pck.Workbook.Worksheets.Add("Cantidad Smart");
                    ws.Cells["A1"].LoadFromDataTable(filtered, true, OfficeOpenXml.Table.TableStyles.Light1);
                    ws.Cells[ws.Dimension.Address].AutoFitColumns();
                    ws.Column(4).Style.Numberformat.Format = "yyyy-mm-dd";

                    ExcelWorksheet ws1 = pck.Workbook.Worksheets.Add("DetalleSmart");
                    ws1.Cells["A1"].LoadFromDataTable(filtered1, true, OfficeOpenXml.Table.TableStyles.Light1);
                    ws1.Cells[ws1.Dimension.Address].AutoFitColumns();
                    ws1.Column(21).Style.Numberformat.Format = "yyyy-mm-dd";
                    ws1.Column(25).Style.Numberformat.Format = "yyyy-mm-dd";

                    ExcelWorksheet ws2 = pck.Workbook.Worksheets.Add("DetalleTeams");
                    ws2.Cells["A1"].LoadFromDataTable(filtered2, true, OfficeOpenXml.Table.TableStyles.Light1);
                    ws2.Cells[ws2.Dimension.Address].AutoFitColumns();
                    ws2.Column(4).Style.Numberformat.Format = "yyyy-mm-dd";

                    pck.Save();
                }
                MessageBox.Show("Tu archivo esta listo en la carpeta " + folderLocation);
            }
        }

        private void ExportarDataGridViewExcel(DataGridView grd)
        {

            if (grd.DataSource != null)
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                var dt = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string folderLocation = "C:\\ReporteTeams\\";
                bool exists = System.IO.Directory.Exists(folderLocation);

                if (!exists)
                    System.IO.Directory.CreateDirectory(folderLocation);


                System.IO.FileInfo file = new System.IO.FileInfo(folderLocation + "\\Reporte" + dt + ".xlsx");
                using (ExcelPackage pck = new ExcelPackage(file))
                {
                    //BindingSource bs = (BindingSource)grd.DataSource;
                    DataTable table = (DataTable)grd.DataSource;
                    System.Data.DataTable filtered = table.DefaultView.ToTable();

                    ExcelWorksheet ws = pck.Workbook.Worksheets.Add("Sheet1");
                    ws.Cells["A1"].LoadFromDataTable(((System.Data.DataTable)filtered), true, OfficeOpenXml.Table.TableStyles.Light1);
                    ws.Cells[ws.Dimension.Address].AutoFitColumns();
                    ws.Column(4).Style.Numberformat.Format = "yyyy-mm-dd";

                    pck.Save();
                }
                MessageBox.Show("Tu archivo esta listo en la carpeta " + folderLocation);
            }
        }

        private void FrmInicio_Load(object sender, EventArgs e)
        {
            lblStatus.Text = "Listo";

        }

        private void EquiposInactivosToolStripMenuItem_Click(object sender, EventArgs e)
        {

            dataGridView1.DataSource = clspase.ConsultarDB(33);
            dataGridView1.AutoResizeColumns();
        }

        private void EquiposActivosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //AppDatos clspase = new AppDatos();
            dataGridView1.DataSource = clspase.ConsultarDB(34, 0);
            dataGridView1.AutoResizeColumns();
        }

        private void BtnBuscarSeccion_Click(object sender, EventArgs e)
        {

            if (txtCodigoSeccion.Text == null || txtCodigoSeccion.Text == "")
            {
                MessageBox.Show("El código a buscar no puede estar vacio");
            }
            else
            {
                gbSeccion.Visible = true;

                //TraerDataSmart();
                DataTable dt = new DataTable();
                dt = clspase.ConsultaTeams(23, txtCodigoSeccion.Text);
                if (dt.Rows.Count != 0)
                {
                    lblSede.Text = dt.Rows[0][1].ToString();
                    lblUnidadNegocio.Text = dt.Rows[0][5].ToString();
                    lblUnidadAcademica.Text = dt.Rows[0][7].ToString();
                    lblProducto.Text = dt.Rows[0][11].ToString();
                    lblSemestre.Text = dt.Rows[0][13].ToString();
                    lblCurso.Text = dt.Rows[0][18].ToString();
                    lblcodigo.Text = dt.Rows[0][20].ToString() + " - " + dt.Rows[0][21].ToString(); ;

                    toolTip1.SetToolTip(lblProducto, dt.Rows[0][11].ToString());
                    toolTip1.SetToolTip(lblCurso, dt.Rows[0][18].ToString());
                    idCurso = int.Parse(dt.Rows[0][16].ToString());

                    DataTable ds = new DataTable();
                    ds = clspase.ConsultarDB(14, idCurso);

                    if (ds.Rows.Count == 0)
                    {
                        btnProcesar.Enabled = false;
                        btnNuevoDemanda.Enabled = true;
                        chkAgregarMiembros.Checked = false;
                        chkGenerarAgendas.Checked = false;
                        chkAgregarMiembros.Enabled = false;
                        chkGenerarAgendas.Enabled = false;
                    }
                    else
                    {
                        btnProcesar.Enabled = true;
                        btnNuevoDemanda.Enabled = false;
                        chkAgregarMiembros.Checked = false;
                        chkGenerarAgendas.Checked = false;
                        chkAgregarMiembros.Enabled = true;
                        chkGenerarAgendas.Enabled = true;
                    }
                }
                else
                {
                    MessageBox.Show("No se encontro el horario solicitado");
                }
            }
        }

        private async void BtnProcesar_Click(object sender, EventArgs e)
        {


            if (idCurso == 0)
            {
                MessageBox.Show("Error");
            }
            else
            {
                Cursor.Current = Cursors.WaitCursor;
                lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();

                CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
                DataTable dt = await creacion.ActualizarTeamsAsync(boolAgregarMiembros, boolGenerarAgendas);
                DataTable links = creacion.TraerLinks();
                dataGridView1.DataSource = links;
                lblStatus.Text = "Proceso terminado: " + DateTime.Now.ToString();

                tspbProgreso.Visible = false;
                Cursor.Current = Cursors.Default;
                MessageBox.Show("Proceso terminado");
            }

        }

        private async void BtnNuevoDemanda_Click(object sender, EventArgs e)
        {
            if (idCurso == 0)
            {
                MessageBox.Show("Error: Revisa el codigo de horario");
            }
            else
            {
                Cursor.Current = Cursors.WaitCursor;
                CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
                lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();
                tspbProgreso.Value = 50;
                if (await creacion.CrearTeams())
                {
                    btnProcesar.Enabled = false;
                    MessageBox.Show("Recuerda que debes activar el equipo creado antes de agregar miembros y enviar agendas");
                    btnBuscarSeccion.Enabled = false;
                    btnActualizarTeams.Enabled = false;
                    lblStatus.Text = "Proceso terminado: " + DateTime.Now.ToString();
                    tspbProgreso.Value = 100;
                    Cursor.Current = Cursors.Default;
                    btnNuevoDemanda.Enabled = false;
                    MessageBox.Show("Proceso terminado");
                }


            }

        }

        private async void BtnBorrarAgendas_ClickAsync(object sender, EventArgs e)
        {
            idCurso = 0;
            CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
            DataTable groups = await creacion.DeleteEvents();
        }

        private void BtnBloqueo_Click(object sender, EventArgs e)
        {
            if (btnBloqueo.Text == "Desbloquear")
                if (textBox1.Text == ConstantsAPI.PASS_APP)
                {
                    btnBloqueo.Text = "Bloquear";
                    btnCrearTeam.Enabled = true;
                    btnActualizarTeams.Enabled = true;
                }
                else
                {
                    MessageBox.Show("Clave Incorrecta");
                    btnCrearTeam.Enabled = false;
                    btnActualizarTeams.Enabled = false;
                }
            else
            {
                if (btnBloqueo.Text == "Bloquear")
                {
                    btnBloqueo.Text = "Desbloquear";
                    btnCrearTeam.Enabled = false;
                    btnActualizarTeams.Enabled = false;
                }
            }

        }
        bool boolAgregarMiembros = false;
        bool boolGenerarAgendas = false;


        private void ChkAgregarMiembros_CheckedChanged(object sender, EventArgs e)
        {
            boolAgregarMiembros = chkAgregarMiembros.Checked;
        }

        private void ChkGenerarAgendas_CheckedChanged(object sender, EventArgs e)
        {
            boolGenerarAgendas = chkGenerarAgendas.Checked;
        }

        private void BtnChequearMiembros_Click(object sender, EventArgs e)
        {
            if (idCurso == 0)
            {
                MessageBox.Show("Error: Revisa el codigo de horario");
            }
            else
            {
                CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
                //DataTable dt = await creacion.ChequearMiembrosTeams();
                MessageBox.Show("Termino");
            }
        }

        private void ReporteDeHorariosToolStripMenuItem_ClickAsync(object sender, EventArgs e)
        {
            string varNroDias = Microsoft.VisualBasic.Interaction.InputBox("Escribe el numero de días que deseas el reporte a partir de hoy", "Número de Días", "1", 100, 0);
            int nroDias = int.Parse(varNroDias);
            CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
            DataTable dt = creacion.ReporteHorarios(nroDias);
            dataGridView1.DataSource = dt;
        }

        private async void BtnReporteMiembros_ClickAsync(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            lblStatus.Text = "Procesando desde: " + DateTime.Now.ToString();
            CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(idCurso);
            DataTable dt = creacion.ReporteMiembros();
            DataSet result = new DataSet();
            tspbProgreso.Maximum = dt.Rows.Count;
            int valueRow = 0;
            foreach (DataRow row in dt.Rows)
            {

                idCurso = int.Parse(row[18].ToString());
                try
                {
                    result = await creacion.ReporteMiembrosDetalleAsync(idCurso);
                }
                catch (Exception err)
                {
                    log.Error(err);
                }
                valueRow++;
            }

            dataGridView1.DataSource = dt;
            dataGridView1.AutoResizeColumns();
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView2.DataSource = result.Tables[1];
            dataGridView2.AutoResizeColumns();
            dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView3.DataSource = result.Tables[0];
            dataGridView3.AutoResizeColumns();
            dataGridView3.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            opReporte = 1;
            Cursor.Current = Cursors.Default;
            MessageBox.Show("Proceso terminado");
            //dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.Fill);
        }
        int opReporte = 0;
        private async void ReporteDeMiembrosToolStripMenuItem_ClickAsync(object sender, EventArgs e)
        {
            CreaciondeEquiposaDemanda creacion = new CreaciondeEquiposaDemanda(0);
            DataTable dt = creacion.ReporteMiembros();
            DataTable detalleSmart = new DataTable();
            DataTable detalleTeams = new DataTable();
            foreach (DataRow row in dt.Rows)
            {
                idCurso = int.Parse(row[18].ToString());
                try
                {
                    DataSet result = await creacion.ReporteMiembrosDetalleAsync(idCurso);
                    detalleSmart.Merge(result.Tables[1]);
                    detalleTeams.Merge(result.Tables[0]);
                }
                catch (Exception err)
                {

                    log.Error(err);
                }
            }

            dataGridView1.DataSource = dt;
            dataGridView1.AutoResizeColumns();
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView2.DataSource = detalleSmart;
            dataGridView2.AutoResizeColumns();
            dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView3.DataSource = detalleTeams;
            dataGridView3.AutoResizeColumns();
            dataGridView3.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            opReporte = 1;
        }

        private void depuraciónEquiposToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable ds = new DataTable();
                ds = clspase.ConsultarDB(41);
                int conteo = 0;
                if (ds.Rows.Count != 0)
                {
                    foreach (DataRow row in ds.Rows)
                    {
                        idCurso = int.Parse(row["IdSeccionSmart"].ToString());
                        conteo++;
                    }
                    log.Debug(conteo);


                }
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}
