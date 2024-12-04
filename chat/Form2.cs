using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Data.Sqlite;

namespace chat
{
    public partial class Form2 : Form
    {

        private Form1 form1;

        public Form2(Form1 form1)
        {



            
            InitializeComponent();

            this.form1=form1;

            label1.Text = "Введите ваше имя (ник):";
            label2.Text = "Ваш идентификатор: \n " + Environment.MachineName;
            button1.Text = "Подтвердить";
            label3.Text = "Ip";
            label4.Text = "Port:";
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string filePath = "settings.xml";
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = true
            };

            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("IdentAndSettings");

                writer.WriteStartElement("nic");
                writer.WriteString(textBox1.Text);
                writer.WriteEndElement();

                writer.WriteStartElement("idPC");
                writer.WriteString(Environment.MachineName);
                writer.WriteEndElement();

                writer.WriteStartElement("ip");
                writer.WriteString(textBox2.Text);
                writer.WriteEndElement();

                writer.WriteStartElement("port");
                writer.WriteString("8080");
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }



            form1.Button1.Visible = false;
            this.Close();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            textBox2.Text = "localhost";
            textBox3.Text = "8080";
        }
        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Разрешаем вводимые символы: цифры, точка и управление (например, Backspace)
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
            {
                e.Handled = true; // Отказать во вводе
            }

            // Дополнительная проверка, чтобы не вводить более трех точек
            if (e.KeyChar == '.')
            {
                if (textBox2.Text.Count(c => c == '.') >= 3)
                {
                    e.Handled = true; // Отказать вводу, если уже три точки
                }
            }
        }
        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Разрешаем вводимые символы: только цифры и управление (например, Backspace)
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true; // Отказать во вводе
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
    }
}
