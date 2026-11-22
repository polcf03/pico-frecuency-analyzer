namespace Fecuencimetro_Visualizador
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            ComboBox_Com = new ComboBox();
            Bt_Connect = new Button();
            label2 = new Label();
            textBox_FrecGen = new TextBox();
            textBox_DutyCycleGen = new TextBox();
            label3 = new Label();
            Bt_set = new Button();
            dataGridView_visualizador = new DataGridView();
            comboBox_Escala = new ComboBox();
            label4 = new Label();
            textBox_frecMed = new TextBox();
            label5 = new Label();
            textBox_Periodo = new TextBox();
            label6 = new Label();
            textBox_dutycycleMed = new TextBox();
            label7 = new Label();
            textBox_cnt = new TextBox();
            label8 = new Label();
            bt_rst = new Button();
            textBox_targetfrec = new TextBox();
            label9 = new Label();
            bt_CSV = new Button();
            textBox_targetdutycicle = new TextBox();
            label10 = new Label();
            btnPausarContador = new Button();
            ((System.ComponentModel.ISupportInitialize)dataGridView_visualizador).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 14);
            label1.Name = "label1";
            label1.Size = new Size(72, 20);
            label1.TabIndex = 1;
            label1.Text = "Port Serie";
            // 
            // ComboBox_Com
            // 
            ComboBox_Com.FormattingEnabled = true;
            ComboBox_Com.Location = new Point(12, 36);
            ComboBox_Com.Name = "ComboBox_Com";
            ComboBox_Com.Size = new Size(103, 28);
            ComboBox_Com.TabIndex = 2;
            // 
            // Bt_Connect
            // 
            Bt_Connect.Location = new Point(123, 36);
            Bt_Connect.Name = "Bt_Connect";
            Bt_Connect.Size = new Size(76, 29);
            Bt_Connect.TabIndex = 3;
            Bt_Connect.Text = "Connect";
            Bt_Connect.UseVisualStyleBackColor = true;
            Bt_Connect.Click += Bt_Connect_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(205, 15);
            label2.Name = "label2";
            label2.Size = new Size(107, 20);
            label2.TabIndex = 4;
            label2.Text = "Frec. Generada";
            // 
            // textBox_FrecGen
            // 
            textBox_FrecGen.Location = new Point(205, 36);
            textBox_FrecGen.Name = "textBox_FrecGen";
            textBox_FrecGen.Size = new Size(125, 27);
            textBox_FrecGen.TabIndex = 5;
            textBox_FrecGen.TextAlign = HorizontalAlignment.Right;
            // 
            // textBox_DutyCycleGen
            // 
            textBox_DutyCycleGen.Location = new Point(336, 36);
            textBox_DutyCycleGen.Name = "textBox_DutyCycleGen";
            textBox_DutyCycleGen.Size = new Size(125, 27);
            textBox_DutyCycleGen.TabIndex = 6;
            textBox_DutyCycleGen.TextAlign = HorizontalAlignment.Right;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(336, 15);
            label3.Name = "label3";
            label3.Size = new Size(112, 20);
            label3.TabIndex = 7;
            label3.Text = "Duty Cycle Gen.";
            // 
            // Bt_set
            // 
            Bt_set.Location = new Point(467, 35);
            Bt_set.Name = "Bt_set";
            Bt_set.Size = new Size(43, 29);
            Bt_set.TabIndex = 8;
            Bt_set.Text = "Set";
            Bt_set.UseVisualStyleBackColor = true;
            Bt_set.Click += Bt_set_Click;
            // 
            // dataGridView_visualizador
            // 
            dataGridView_visualizador.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView_visualizador.Location = new Point(12, 124);
            dataGridView_visualizador.Name = "dataGridView_visualizador";
            dataGridView_visualizador.ReadOnly = true;
            dataGridView_visualizador.RowHeadersWidth = 51;
            dataGridView_visualizador.Size = new Size(813, 644);
            dataGridView_visualizador.TabIndex = 9;
            // 
            // comboBox_Escala
            // 
            comboBox_Escala.FormattingEnabled = true;
            comboBox_Escala.Location = new Point(12, 90);
            comboBox_Escala.Name = "comboBox_Escala";
            comboBox_Escala.Size = new Size(103, 28);
            comboBox_Escala.TabIndex = 10;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(12, 67);
            label4.Name = "label4";
            label4.Size = new Size(50, 20);
            label4.TabIndex = 11;
            label4.Text = "Escala";
            // 
            // textBox_frecMed
            // 
            textBox_frecMed.Location = new Point(123, 91);
            textBox_frecMed.Name = "textBox_frecMed";
            textBox_frecMed.ReadOnly = true;
            textBox_frecMed.Size = new Size(125, 27);
            textBox_frecMed.TabIndex = 12;
            textBox_frecMed.TextAlign = HorizontalAlignment.Center;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(123, 68);
            label5.Name = "label5";
            label5.Size = new Size(94, 20);
            label5.TabIndex = 13;
            label5.Text = "Frec. Medida";
            // 
            // textBox_Periodo
            // 
            textBox_Periodo.Location = new Point(254, 91);
            textBox_Periodo.Name = "textBox_Periodo";
            textBox_Periodo.ReadOnly = true;
            textBox_Periodo.Size = new Size(125, 27);
            textBox_Periodo.TabIndex = 14;
            textBox_Periodo.TextAlign = HorizontalAlignment.Center;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(254, 68);
            label6.Name = "label6";
            label6.Size = new Size(60, 20);
            label6.TabIndex = 15;
            label6.Text = "Periodo";
            // 
            // textBox_dutycycleMed
            // 
            textBox_dutycycleMed.Location = new Point(385, 91);
            textBox_dutycycleMed.Name = "textBox_dutycycleMed";
            textBox_dutycycleMed.ReadOnly = true;
            textBox_dutycycleMed.Size = new Size(125, 27);
            textBox_dutycycleMed.TabIndex = 16;
            textBox_dutycycleMed.TextAlign = HorizontalAlignment.Center;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(385, 68);
            label7.Name = "label7";
            label7.Size = new Size(116, 20);
            label7.TabIndex = 17;
            label7.Text = "Duty Cycle Med.";
            // 
            // textBox_cnt
            // 
            textBox_cnt.Location = new Point(516, 91);
            textBox_cnt.Name = "textBox_cnt";
            textBox_cnt.ReadOnly = true;
            textBox_cnt.Size = new Size(125, 27);
            textBox_cnt.TabIndex = 18;
            textBox_cnt.TextAlign = HorizontalAlignment.Center;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(516, 68);
            label8.Name = "label8";
            label8.Size = new Size(71, 20);
            label8.TabIndex = 19;
            label8.Text = "Contador";
            // 
            // bt_rst
            // 
            bt_rst.Location = new Point(747, 88);
            bt_rst.Name = "bt_rst";
            bt_rst.Size = new Size(78, 30);
            bt_rst.TabIndex = 20;
            bt_rst.Text = "Reset";
            bt_rst.UseVisualStyleBackColor = true;
            bt_rst.Click += bt_rst_Click;
            // 
            // textBox_targetfrec
            // 
            textBox_targetfrec.Location = new Point(516, 34);
            textBox_targetfrec.Name = "textBox_targetfrec";
            textBox_targetfrec.Size = new Size(125, 27);
            textBox_targetfrec.TabIndex = 21;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(516, 14);
            label9.Name = "label9";
            label9.Size = new Size(84, 20);
            label9.TabIndex = 22;
            label9.Text = "Target Frec.";
            // 
            // bt_CSV
            // 
            bt_CSV.Location = new Point(786, 33);
            bt_CSV.Name = "bt_CSV";
            bt_CSV.Size = new Size(45, 29);
            bt_CSV.TabIndex = 23;
            bt_CSV.Text = "CSV";
            bt_CSV.UseVisualStyleBackColor = true;
            bt_CSV.Click += bt_CSV_Click;
            // 
            // textBox_targetdutycicle
            // 
            textBox_targetdutycicle.Location = new Point(655, 34);
            textBox_targetdutycicle.Name = "textBox_targetdutycicle";
            textBox_targetdutycicle.Size = new Size(125, 27);
            textBox_targetdutycicle.TabIndex = 24;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(655, 14);
            label10.Name = "label10";
            label10.Size = new Size(124, 20);
            label10.TabIndex = 25;
            label10.Text = "Target Duty Cycle";
            // 
            // btnPausarContador
            // 
            btnPausarContador.Location = new Point(647, 89);
            btnPausarContador.Name = "btnPausarContador";
            btnPausarContador.Size = new Size(94, 29);
            btnPausarContador.TabIndex = 26;
            btnPausarContador.Text = "Pausar";
            btnPausarContador.UseVisualStyleBackColor = true;
            btnPausarContador.Click += btnPausarContador_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(838, 780);
            Controls.Add(btnPausarContador);
            Controls.Add(label10);
            Controls.Add(textBox_targetdutycicle);
            Controls.Add(bt_CSV);
            Controls.Add(label9);
            Controls.Add(textBox_targetfrec);
            Controls.Add(bt_rst);
            Controls.Add(label8);
            Controls.Add(textBox_cnt);
            Controls.Add(label7);
            Controls.Add(textBox_dutycycleMed);
            Controls.Add(label6);
            Controls.Add(textBox_Periodo);
            Controls.Add(label5);
            Controls.Add(textBox_frecMed);
            Controls.Add(label4);
            Controls.Add(comboBox_Escala);
            Controls.Add(dataGridView_visualizador);
            Controls.Add(Bt_set);
            Controls.Add(label3);
            Controls.Add(textBox_DutyCycleGen);
            Controls.Add(textBox_FrecGen);
            Controls.Add(label2);
            Controls.Add(Bt_Connect);
            Controls.Add(ComboBox_Com);
            Controls.Add(label1);
            Name = "Form1";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridView_visualizador).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Label label1;
        private ComboBox ComboBox_Com;
        private Button Bt_Connect;
        private Label label2;
        private TextBox textBox_FrecGen;
        private TextBox textBox_DutyCycleGen;
        private Label label3;
        private Button Bt_set;
        private DataGridView dataGridView_visualizador;
        private ComboBox comboBox_Escala;
        private Label label4;
        private TextBox textBox_frecMed;
        private Label label5;
        private TextBox textBox_Periodo;
        private Label label6;
        private TextBox textBox_dutycycleMed;
        private Label label7;
        private TextBox textBox_cnt;
        private Label label8;
        private Button bt_rst;
        private TextBox textBox_targetfrec;
        private Label label9;
        private Button bt_CSV;
        private TextBox textBox_targetdutycicle;
        private Label label10;
        private Button btnPausarContador;
    }
}
