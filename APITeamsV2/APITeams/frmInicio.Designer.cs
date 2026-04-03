using System;

namespace APITeams
{
    partial class frmInicio
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmInicio));
            this.panel3 = new System.Windows.Forms.Panel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnChequearMiembros = new System.Windows.Forms.Button();
            this.btnBloqueo = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.btnBorrarAgendas = new System.Windows.Forms.Button();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.procesosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ejecutarTodoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reportesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.teamsActivosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.equiposInactivosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.equiposActivosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reporteDeHorariosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reporteDeMiembrosToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.depuraciónEquiposToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gbSeccion = new System.Windows.Forms.GroupBox();
            this.btnReporteMiembros = new System.Windows.Forms.Button();
            this.chkGenerarAgendas = new System.Windows.Forms.CheckBox();
            this.chkAgregarMiembros = new System.Windows.Forms.CheckBox();
            this.btnNuevoDemanda = new System.Windows.Forms.Button();
            this.lblProducto = new System.Windows.Forms.Label();
            this.lblcodigo = new System.Windows.Forms.Label();
            this.lblSemestre = new System.Windows.Forms.Label();
            this.btnProcesar = new System.Windows.Forms.Button();
            this.lblCurso = new System.Windows.Forms.Label();
            this.lblUnidadAcademica = new System.Windows.Forms.Label();
            this.lblUnidadNegocio = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblSede = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnBuscarSeccion = new System.Windows.Forms.Button();
            this.txtCodigoSeccion = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnCrearTeam = new System.Windows.Forms.Button();
            this.btnCrearAgendas = new System.Windows.Forms.Button();
            this.btnActualizarTeams = new System.Windows.Forms.Button();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.dataGridView2 = new System.Windows.Forms.DataGridView();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.dataGridView3 = new System.Windows.Forms.DataGridView();
            this.btnExportar = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.tspbProgreso = new System.Windows.Forms.ToolStripProgressBar();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.gbSeccion.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).BeginInit();
            this.tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView3)).BeginInit();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.splitContainer1);
            this.panel3.Controls.Add(this.statusStrip1);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Margin = new System.Windows.Forms.Padding(6);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1968, 1079);
            this.panel3.TabIndex = 5;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(6);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.btnChequearMiembros);
            this.splitContainer1.Panel1.Controls.Add(this.btnBloqueo);
            this.splitContainer1.Panel1.Controls.Add(this.textBox1);
            this.splitContainer1.Panel1.Controls.Add(this.btnBorrarAgendas);
            this.splitContainer1.Panel1.Controls.Add(this.menuStrip1);
            this.splitContainer1.Panel1.Controls.Add(this.gbSeccion);
            this.splitContainer1.Panel1.Controls.Add(this.btnBuscarSeccion);
            this.splitContainer1.Panel1.Controls.Add(this.txtCodigoSeccion);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.btnCrearTeam);
            this.splitContainer1.Panel1.Controls.Add(this.btnCrearAgendas);
            this.splitContainer1.Panel1.Controls.Add(this.btnActualizarTeams);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(1968, 1057);
            this.splitContainer1.SplitterDistance = 360;
            this.splitContainer1.SplitterWidth = 8;
            this.splitContainer1.TabIndex = 11;
            // 
            // btnChequearMiembros
            // 
            this.btnChequearMiembros.Location = new System.Drawing.Point(1380, 306);
            this.btnChequearMiembros.Margin = new System.Windows.Forms.Padding(6);
            this.btnChequearMiembros.Name = "btnChequearMiembros";
            this.btnChequearMiembros.Size = new System.Drawing.Size(268, 44);
            this.btnChequearMiembros.TabIndex = 40;
            this.btnChequearMiembros.Text = "Chequear Miembros";
            this.btnChequearMiembros.UseVisualStyleBackColor = true;
            this.btnChequearMiembros.Visible = false;
            this.btnChequearMiembros.Click += new System.EventHandler(this.BtnChequearMiembros_Click);
            // 
            // btnBloqueo
            // 
            this.btnBloqueo.Location = new System.Drawing.Point(768, 304);
            this.btnBloqueo.Margin = new System.Windows.Forms.Padding(6);
            this.btnBloqueo.Name = "btnBloqueo";
            this.btnBloqueo.Size = new System.Drawing.Size(184, 44);
            this.btnBloqueo.TabIndex = 39;
            this.btnBloqueo.Text = "Desbloquear";
            this.btnBloqueo.UseVisualStyleBackColor = true;
            this.btnBloqueo.Visible = false;
            this.btnBloqueo.Click += new System.EventHandler(this.BtnBloqueo_Click);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(964, 304);
            this.textBox1.Margin = new System.Windows.Forms.Padding(6);
            this.textBox1.Name = "textBox1";
            this.textBox1.PasswordChar = '*';
            this.textBox1.Size = new System.Drawing.Size(196, 31);
            this.textBox1.TabIndex = 38;
            this.textBox1.Visible = false;
            // 
            // btnBorrarAgendas
            // 
            this.btnBorrarAgendas.Location = new System.Drawing.Point(1694, 306);
            this.btnBorrarAgendas.Margin = new System.Windows.Forms.Padding(6);
            this.btnBorrarAgendas.Name = "btnBorrarAgendas";
            this.btnBorrarAgendas.Size = new System.Drawing.Size(268, 44);
            this.btnBorrarAgendas.TabIndex = 37;
            this.btnBorrarAgendas.Text = "Borrar Agendas";
            this.btnBorrarAgendas.UseVisualStyleBackColor = true;
            this.btnBorrarAgendas.Visible = false;
            this.btnBorrarAgendas.Click += new System.EventHandler(this.BtnBorrarAgendas_ClickAsync);
            // 
            // menuStrip1
            // 
            this.menuStrip1.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.procesosToolStripMenuItem,
            this.reportesToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1968, 40);
            this.menuStrip1.TabIndex = 36;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // procesosToolStripMenuItem
            // 
            this.procesosToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ejecutarTodoToolStripMenuItem});
            this.procesosToolStripMenuItem.Name = "procesosToolStripMenuItem";
            this.procesosToolStripMenuItem.Size = new System.Drawing.Size(128, 36);
            this.procesosToolStripMenuItem.Text = "Procesos";
            // 
            // ejecutarTodoToolStripMenuItem
            // 
            this.ejecutarTodoToolStripMenuItem.Name = "ejecutarTodoToolStripMenuItem";
            this.ejecutarTodoToolStripMenuItem.Size = new System.Drawing.Size(293, 44);
            this.ejecutarTodoToolStripMenuItem.Text = "Ejecutar Todo";
            // 
            // reportesToolStripMenuItem
            // 
            this.reportesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.teamsActivosToolStripMenuItem});
            this.reportesToolStripMenuItem.Name = "reportesToolStripMenuItem";
            this.reportesToolStripMenuItem.Size = new System.Drawing.Size(128, 36);
            this.reportesToolStripMenuItem.Text = "Reportes";
            // 
            // teamsActivosToolStripMenuItem
            // 
            this.teamsActivosToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.equiposInactivosToolStripMenuItem,
            this.equiposActivosToolStripMenuItem,
            this.reporteDeHorariosToolStripMenuItem,
            this.reporteDeMiembrosToolStripMenuItem,
            this.depuraciónEquiposToolStripMenuItem});
            this.teamsActivosToolStripMenuItem.Name = "teamsActivosToolStripMenuItem";
            this.teamsActivosToolStripMenuItem.Size = new System.Drawing.Size(316, 44);
            this.teamsActivosToolStripMenuItem.Text = "Reportes Teams";
            // 
            // equiposInactivosToolStripMenuItem
            // 
            this.equiposInactivosToolStripMenuItem.Name = "equiposInactivosToolStripMenuItem";
            this.equiposInactivosToolStripMenuItem.Size = new System.Drawing.Size(381, 44);
            this.equiposInactivosToolStripMenuItem.Text = "Equipos Inactivos";
            this.equiposInactivosToolStripMenuItem.Click += new System.EventHandler(this.EquiposInactivosToolStripMenuItem_Click);
            // 
            // equiposActivosToolStripMenuItem
            // 
            this.equiposActivosToolStripMenuItem.Name = "equiposActivosToolStripMenuItem";
            this.equiposActivosToolStripMenuItem.Size = new System.Drawing.Size(381, 44);
            this.equiposActivosToolStripMenuItem.Text = "Equipos Activos";
            this.equiposActivosToolStripMenuItem.Click += new System.EventHandler(this.EquiposActivosToolStripMenuItem_Click);
            // 
            // reporteDeHorariosToolStripMenuItem
            // 
            this.reporteDeHorariosToolStripMenuItem.Name = "reporteDeHorariosToolStripMenuItem";
            this.reporteDeHorariosToolStripMenuItem.Size = new System.Drawing.Size(381, 44);
            this.reporteDeHorariosToolStripMenuItem.Text = "Reporte de Horarios";
            this.reporteDeHorariosToolStripMenuItem.Click += new System.EventHandler(this.ReporteDeHorariosToolStripMenuItem_ClickAsync);
            // 
            // reporteDeMiembrosToolStripMenuItem
            // 
            this.reporteDeMiembrosToolStripMenuItem.Name = "reporteDeMiembrosToolStripMenuItem";
            this.reporteDeMiembrosToolStripMenuItem.Size = new System.Drawing.Size(381, 44);
            this.reporteDeMiembrosToolStripMenuItem.Text = "Reporte de Miembros";
            this.reporteDeMiembrosToolStripMenuItem.Click += new System.EventHandler(this.ReporteDeMiembrosToolStripMenuItem_ClickAsync);
            // 
            // depuraciónEquiposToolStripMenuItem
            // 
            this.depuraciónEquiposToolStripMenuItem.Name = "depuraciónEquiposToolStripMenuItem";
            this.depuraciónEquiposToolStripMenuItem.Size = new System.Drawing.Size(381, 44);
            this.depuraciónEquiposToolStripMenuItem.Text = "Depuración Equipos";
            this.depuraciónEquiposToolStripMenuItem.Click += new System.EventHandler(this.depuraciónEquiposToolStripMenuItem_ClickAsync);
            // 
            // gbSeccion
            // 
            this.gbSeccion.Controls.Add(this.btnReporteMiembros);
            this.gbSeccion.Controls.Add(this.chkGenerarAgendas);
            this.gbSeccion.Controls.Add(this.chkAgregarMiembros);
            this.gbSeccion.Controls.Add(this.btnNuevoDemanda);
            this.gbSeccion.Controls.Add(this.lblProducto);
            this.gbSeccion.Controls.Add(this.lblcodigo);
            this.gbSeccion.Controls.Add(this.lblSemestre);
            this.gbSeccion.Controls.Add(this.btnProcesar);
            this.gbSeccion.Controls.Add(this.lblCurso);
            this.gbSeccion.Controls.Add(this.lblUnidadAcademica);
            this.gbSeccion.Controls.Add(this.lblUnidadNegocio);
            this.gbSeccion.Controls.Add(this.label7);
            this.gbSeccion.Controls.Add(this.label6);
            this.gbSeccion.Controls.Add(this.label8);
            this.gbSeccion.Controls.Add(this.label5);
            this.gbSeccion.Controls.Add(this.label4);
            this.gbSeccion.Controls.Add(this.label3);
            this.gbSeccion.Controls.Add(this.lblSede);
            this.gbSeccion.Controls.Add(this.label2);
            this.gbSeccion.Location = new System.Drawing.Point(354, 23);
            this.gbSeccion.Margin = new System.Windows.Forms.Padding(6);
            this.gbSeccion.Name = "gbSeccion";
            this.gbSeccion.Padding = new System.Windows.Forms.Padding(6);
            this.gbSeccion.Size = new System.Drawing.Size(1590, 269);
            this.gbSeccion.TabIndex = 19;
            this.gbSeccion.TabStop = false;
            this.gbSeccion.Text = "Sección";
            this.gbSeccion.Visible = false;
            // 
            // btnReporteMiembros
            // 
            this.btnReporteMiembros.Location = new System.Drawing.Point(22, 213);
            this.btnReporteMiembros.Margin = new System.Windows.Forms.Padding(6);
            this.btnReporteMiembros.Name = "btnReporteMiembros";
            this.btnReporteMiembros.Size = new System.Drawing.Size(248, 44);
            this.btnReporteMiembros.TabIndex = 41;
            this.btnReporteMiembros.Text = "Miembros";
            this.btnReporteMiembros.UseVisualStyleBackColor = true;
            this.btnReporteMiembros.Click += new System.EventHandler(this.BtnReporteMiembros_ClickAsync);
            // 
            // chkGenerarAgendas
            // 
            this.chkGenerarAgendas.AutoSize = true;
            this.chkGenerarAgendas.Location = new System.Drawing.Point(1372, 225);
            this.chkGenerarAgendas.Margin = new System.Windows.Forms.Padding(6);
            this.chkGenerarAgendas.Name = "chkGenerarAgendas";
            this.chkGenerarAgendas.Size = new System.Drawing.Size(213, 29);
            this.chkGenerarAgendas.TabIndex = 58;
            this.chkGenerarAgendas.Text = "Generar Agendas";
            this.chkGenerarAgendas.UseVisualStyleBackColor = true;
            this.chkGenerarAgendas.Visible = false;
            this.chkGenerarAgendas.CheckedChanged += new System.EventHandler(this.ChkGenerarAgendas_CheckedChanged);
            // 
            // chkAgregarMiembros
            // 
            this.chkAgregarMiembros.AutoSize = true;
            this.chkAgregarMiembros.Location = new System.Drawing.Point(1138, 225);
            this.chkAgregarMiembros.Margin = new System.Windows.Forms.Padding(6);
            this.chkAgregarMiembros.Name = "chkAgregarMiembros";
            this.chkAgregarMiembros.Size = new System.Drawing.Size(220, 29);
            this.chkAgregarMiembros.TabIndex = 57;
            this.chkAgregarMiembros.Text = "Agregar Miembros";
            this.chkAgregarMiembros.UseVisualStyleBackColor = true;
            this.chkAgregarMiembros.CheckedChanged += new System.EventHandler(this.ChkAgregarMiembros_CheckedChanged);
            // 
            // btnNuevoDemanda
            // 
            this.btnNuevoDemanda.Location = new System.Drawing.Point(1266, 158);
            this.btnNuevoDemanda.Margin = new System.Windows.Forms.Padding(6);
            this.btnNuevoDemanda.Name = "btnNuevoDemanda";
            this.btnNuevoDemanda.Size = new System.Drawing.Size(150, 44);
            this.btnNuevoDemanda.TabIndex = 55;
            this.btnNuevoDemanda.Text = "Nuevo";
            this.btnNuevoDemanda.UseVisualStyleBackColor = true;
            this.btnNuevoDemanda.Click += new System.EventHandler(this.BtnNuevoDemanda_Click);
            // 
            // lblProducto
            // 
            this.lblProducto.AutoEllipsis = true;
            this.lblProducto.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblProducto.Location = new System.Drawing.Point(824, 31);
            this.lblProducto.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblProducto.Name = "lblProducto";
            this.lblProducto.Size = new System.Drawing.Size(766, 25);
            this.lblProducto.TabIndex = 54;
            this.lblProducto.Text = "lblProducto";
            // 
            // lblcodigo
            // 
            this.lblcodigo.AutoSize = true;
            this.lblcodigo.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblcodigo.Location = new System.Drawing.Point(166, 167);
            this.lblcodigo.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblcodigo.Name = "lblcodigo";
            this.lblcodigo.Size = new System.Drawing.Size(107, 26);
            this.lblcodigo.TabIndex = 53;
            this.lblcodigo.Text = "lblcodigo";
            // 
            // lblSemestre
            // 
            this.lblSemestre.AutoSize = true;
            this.lblSemestre.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSemestre.Location = new System.Drawing.Point(824, 125);
            this.lblSemestre.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblSemestre.Name = "lblSemestre";
            this.lblSemestre.Size = new System.Drawing.Size(139, 26);
            this.lblSemestre.TabIndex = 52;
            this.lblSemestre.Text = "lblSemestre";
            // 
            // btnProcesar
            // 
            this.btnProcesar.Location = new System.Drawing.Point(1428, 158);
            this.btnProcesar.Margin = new System.Windows.Forms.Padding(6);
            this.btnProcesar.Name = "btnProcesar";
            this.btnProcesar.Size = new System.Drawing.Size(150, 44);
            this.btnProcesar.TabIndex = 50;
            this.btnProcesar.Text = "Actualizar";
            this.btnProcesar.UseVisualStyleBackColor = true;
            this.btnProcesar.Click += new System.EventHandler(this.BtnProcesar_Click);
            // 
            // lblCurso
            // 
            this.lblCurso.AutoEllipsis = true;
            this.lblCurso.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurso.Location = new System.Drawing.Point(824, 77);
            this.lblCurso.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCurso.Name = "lblCurso";
            this.lblCurso.Size = new System.Drawing.Size(766, 25);
            this.lblCurso.TabIndex = 48;
            this.lblCurso.Text = "lblCurso";
            // 
            // lblUnidadAcademica
            // 
            this.lblUnidadAcademica.AutoSize = true;
            this.lblUnidadAcademica.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblUnidadAcademica.Location = new System.Drawing.Point(166, 125);
            this.lblUnidadAcademica.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblUnidadAcademica.Name = "lblUnidadAcademica";
            this.lblUnidadAcademica.Size = new System.Drawing.Size(230, 26);
            this.lblUnidadAcademica.TabIndex = 45;
            this.lblUnidadAcademica.Text = "lblUnidadAcademica";
            // 
            // lblUnidadNegocio
            // 
            this.lblUnidadNegocio.AutoSize = true;
            this.lblUnidadNegocio.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblUnidadNegocio.Location = new System.Drawing.Point(162, 77);
            this.lblUnidadNegocio.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblUnidadNegocio.Name = "lblUnidadNegocio";
            this.lblUnidadNegocio.Size = new System.Drawing.Size(199, 26);
            this.lblUnidadNegocio.TabIndex = 44;
            this.lblUnidadNegocio.Text = "lblUnidadNegocio";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(732, 77);
            this.label7.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(81, 25);
            this.label7.TabIndex = 42;
            this.label7.Text = "Curso :";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(698, 123);
            this.label6.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(115, 25);
            this.label6.TabIndex = 41;
            this.label6.Text = "Semestre :";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(16, 167);
            this.label8.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(124, 25);
            this.label8.TabIndex = 43;
            this.label8.Text = "Facilitador :";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(700, 31);
            this.label5.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(110, 25);
            this.label5.TabIndex = 40;
            this.label5.Text = "Producto :";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(16, 121);
            this.label4.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(117, 25);
            this.label4.TabIndex = 39;
            this.label4.Text = "Programa :";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 75);
            this.label3.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(100, 25);
            this.label3.TabIndex = 38;
            this.label3.Text = "División :";
            // 
            // lblSede
            // 
            this.lblSede.AutoSize = true;
            this.lblSede.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSede.Location = new System.Drawing.Point(162, 31);
            this.lblSede.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblSede.Name = "lblSede";
            this.lblSede.Size = new System.Drawing.Size(92, 26);
            this.lblSede.TabIndex = 37;
            this.lblSede.Text = "lblSede";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 31);
            this.label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 25);
            this.label2.TabIndex = 36;
            this.label2.Text = "Sede :";
            // 
            // btnBuscarSeccion
            // 
            this.btnBuscarSeccion.Location = new System.Drawing.Point(238, 137);
            this.btnBuscarSeccion.Margin = new System.Windows.Forms.Padding(6);
            this.btnBuscarSeccion.Name = "btnBuscarSeccion";
            this.btnBuscarSeccion.Size = new System.Drawing.Size(104, 44);
            this.btnBuscarSeccion.TabIndex = 18;
            this.btnBuscarSeccion.Text = "Buscar";
            this.btnBuscarSeccion.UseVisualStyleBackColor = true;
            this.btnBuscarSeccion.Click += new System.EventHandler(this.BtnBuscarSeccion_Click);
            // 
            // txtCodigoSeccion
            // 
            this.txtCodigoSeccion.Location = new System.Drawing.Point(30, 140);
            this.txtCodigoSeccion.Margin = new System.Windows.Forms.Padding(6);
            this.txtCodigoSeccion.Name = "txtCodigoSeccion";
            this.txtCodigoSeccion.Size = new System.Drawing.Size(190, 31);
            this.txtCodigoSeccion.TabIndex = 17;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(24, 87);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(155, 25);
            this.label1.TabIndex = 16;
            this.label1.Text = "Buscar Horario";
            // 
            // btnCrearTeam
            // 
            this.btnCrearTeam.Enabled = false;
            this.btnCrearTeam.Location = new System.Drawing.Point(24, 304);
            this.btnCrearTeam.Margin = new System.Windows.Forms.Padding(6);
            this.btnCrearTeam.Name = "btnCrearTeam";
            this.btnCrearTeam.Size = new System.Drawing.Size(248, 44);
            this.btnCrearTeam.TabIndex = 12;
            this.btnCrearTeam.Text = "Crear Teams";
            this.btnCrearTeam.UseVisualStyleBackColor = true;
            this.btnCrearTeam.Visible = false;
            this.btnCrearTeam.Click += new System.EventHandler(this.BtnCrearTeam_Click);
            // 
            // btnCrearAgendas
            // 
            this.btnCrearAgendas.Enabled = false;
            this.btnCrearAgendas.Location = new System.Drawing.Point(520, 304);
            this.btnCrearAgendas.Margin = new System.Windows.Forms.Padding(6);
            this.btnCrearAgendas.Name = "btnCrearAgendas";
            this.btnCrearAgendas.Size = new System.Drawing.Size(248, 44);
            this.btnCrearAgendas.TabIndex = 14;
            this.btnCrearAgendas.Text = "Crear Agendas";
            this.btnCrearAgendas.UseVisualStyleBackColor = true;
            this.btnCrearAgendas.Visible = false;
            this.btnCrearAgendas.Click += new System.EventHandler(this.BtnCrearAgendas_Click);
            // 
            // btnActualizarTeams
            // 
            this.btnActualizarTeams.Enabled = false;
            this.btnActualizarTeams.Location = new System.Drawing.Point(272, 304);
            this.btnActualizarTeams.Margin = new System.Windows.Forms.Padding(6);
            this.btnActualizarTeams.Name = "btnActualizarTeams";
            this.btnActualizarTeams.Size = new System.Drawing.Size(248, 44);
            this.btnActualizarTeams.TabIndex = 13;
            this.btnActualizarTeams.Text = "Actualizar Teams";
            this.btnActualizarTeams.UseVisualStyleBackColor = true;
            this.btnActualizarTeams.Visible = false;
            this.btnActualizarTeams.Click += new System.EventHandler(this.BtnActualizarTeams_Click);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Margin = new System.Windows.Forms.Padding(6);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.tabControl1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.btnExportar);
            this.splitContainer2.Size = new System.Drawing.Size(1968, 689);
            this.splitContainer2.SplitterDistance = 622;
            this.splitContainer2.SplitterWidth = 8;
            this.splitContainer2.TabIndex = 0;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(6);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1968, 622);
            this.tabControl1.TabIndex = 12;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.dataGridView1);
            this.tabPage1.Location = new System.Drawing.Point(8, 39);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(6);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(6);
            this.tabPage1.Size = new System.Drawing.Size(1952, 575);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Reporte";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(6, 6);
            this.dataGridView1.Margin = new System.Windows.Forms.Padding(6);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 82;
            this.dataGridView1.Size = new System.Drawing.Size(1940, 563);
            this.dataGridView1.TabIndex = 11;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.dataGridView2);
            this.tabPage2.Location = new System.Drawing.Point(8, 39);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(6);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(6);
            this.tabPage2.Size = new System.Drawing.Size(1952, 563);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Detalle Smart";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // dataGridView2
            // 
            this.dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView2.Location = new System.Drawing.Point(6, 6);
            this.dataGridView2.Margin = new System.Windows.Forms.Padding(6);
            this.dataGridView2.Name = "dataGridView2";
            this.dataGridView2.RowHeadersWidth = 82;
            this.dataGridView2.Size = new System.Drawing.Size(1940, 551);
            this.dataGridView2.TabIndex = 13;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.dataGridView3);
            this.tabPage3.Location = new System.Drawing.Point(8, 39);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(6);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(6);
            this.tabPage3.Size = new System.Drawing.Size(1952, 563);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Detalle Teams";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // dataGridView3
            // 
            this.dataGridView3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView3.Location = new System.Drawing.Point(6, 6);
            this.dataGridView3.Margin = new System.Windows.Forms.Padding(6);
            this.dataGridView3.Name = "dataGridView3";
            this.dataGridView3.RowHeadersWidth = 82;
            this.dataGridView3.Size = new System.Drawing.Size(1940, 551);
            this.dataGridView3.TabIndex = 0;
            // 
            // btnExportar
            // 
            this.btnExportar.Location = new System.Drawing.Point(24, 6);
            this.btnExportar.Margin = new System.Windows.Forms.Padding(6);
            this.btnExportar.Name = "btnExportar";
            this.btnExportar.Size = new System.Drawing.Size(162, 44);
            this.btnExportar.TabIndex = 15;
            this.btnExportar.Text = "Exportar";
            this.btnExportar.UseVisualStyleBackColor = true;
            this.btnExportar.Click += new System.EventHandler(this.BtnExportar_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus,
            this.tspbProgreso});
            this.statusStrip1.Location = new System.Drawing.Point(0, 1057);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(2, 0, 28, 0);
            this.statusStrip1.Size = new System.Drawing.Size(1968, 22);
            this.statusStrip1.TabIndex = 10;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(0, 12);
            // 
            // tspbProgreso
            // 
            this.tspbProgreso.Name = "tspbProgreso";
            this.tspbProgreso.Size = new System.Drawing.Size(200, 31);
            this.tspbProgreso.Visible = false;
            // 
            // frmInicio
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1968, 1079);
            this.Controls.Add(this.panel3);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(6);
            this.Name = "frmInicio";
            this.Text = "API Teams ";
            this.Load += new System.EventHandler(this.FrmInicio_Load);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.gbSeccion.ResumeLayout(false);
            this.gbSeccion.PerformLayout();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).EndInit();
            this.tabPage3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView3)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        private void depuraciónEquiposToolStripMenuItem_ClickAsync(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ToolStripProgressBar tspbProgreso;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnCrearTeam;
        private System.Windows.Forms.Button btnCrearAgendas;
        private System.Windows.Forms.Button btnActualizarTeams;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnExportar;
        private System.Windows.Forms.Button btnBuscarSeccion;
        private System.Windows.Forms.TextBox txtCodigoSeccion;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem procesosToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ejecutarTodoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reportesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem teamsActivosToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem equiposInactivosToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem equiposActivosToolStripMenuItem;
        private System.Windows.Forms.GroupBox gbSeccion;
        private System.Windows.Forms.Label lblProducto;
        private System.Windows.Forms.Label lblcodigo;
        private System.Windows.Forms.Label lblSemestre;
        private System.Windows.Forms.Button btnProcesar;
        private System.Windows.Forms.Label lblCurso;
        private System.Windows.Forms.Label lblUnidadAcademica;
        private System.Windows.Forms.Label lblUnidadNegocio;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblSede;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button btnNuevoDemanda;
        private System.Windows.Forms.Button btnBorrarAgendas;
        private System.Windows.Forms.Button btnBloqueo;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.CheckBox chkGenerarAgendas;
        private System.Windows.Forms.CheckBox chkAgregarMiembros;
        private System.Windows.Forms.Button btnChequearMiembros;
        private System.Windows.Forms.ToolStripMenuItem reporteDeHorariosToolStripMenuItem;
        private System.Windows.Forms.Button btnReporteMiembros;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.ToolStripMenuItem reporteDeMiembrosToolStripMenuItem;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.DataGridView dataGridView3;
        private System.Windows.Forms.ToolStripMenuItem depuraciónEquiposToolStripMenuItem;
    }
}

