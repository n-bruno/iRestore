using iTired;
using System;
using System.IO;
using System.Windows.Forms;
using static iCarve.FileManager;

namespace iCarve
{
    public partial class Form_main : Form
    {
        private FileManager fm;
        public Form_main()
        {
            InitializeComponent();
            Text = Constants.PROGRAM_NAME;
        }
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog FD = new OpenFileDialog();
                FD.Filter = "DIMG File| *.dimg";
                if (FD.ShowDialog() == DialogResult.OK)
                {
                    if (File.Exists(FD.FileName))
                    {
                        fm = new FileManager(FD.FileName);

                        if (fm.FATVersion != (int)FAT.Invalid)
                        {
                            button_restore.Enabled = button_carve.Enabled = true;
                            saveToolStripMenuItem.Enabled = false;
                            Text = Constants.PROGRAM_NAME + Constants.SEPERATOR + FD.FileName;
                        }
                        else
                        {
                            MessageBox.Show("Not a FAT image.");
                            fm = null;
                        }
                    }
                }
            }
            catch(FileNotFoundException)
            { MessageBox.Show("The file was not found."); }
            catch (FileLoadException)
            { MessageBox.Show("The file couldn't be loaded."); }
            catch (AccessViolationException)
            { MessageBox.Show("Permission denied."); }
            catch (OutOfMemoryException)
            { MessageBox.Show("Ran out of memmory."); }
        }

        private void button_restore_Click(object sender, EventArgs e)
        {
            int recoverCount = fm.Restore();
            string message;
            switch (recoverCount)
            {
                case 0:
                    message = "No files have been recovered.";
                    break;
                case 1:
                    message = "One file has been recovered.";
                    saveToolStripMenuItem.Enabled = true;
                    break;
                default:
                    message = recoverCount + " files have been recovered.";
                    saveToolStripMenuItem.Enabled = true;
                    break;
            }
            MessageBox.Show(message);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                fm.Save();
                saveToolStripMenuItem.Enabled = false;
            }
            catch (FileNotFoundException)
            { MessageBox.Show("The file was not found."); }
            catch (FileLoadException)
            { MessageBox.Show("The file couldn't be loaded."); }
            catch (AccessViolationException)
            { MessageBox.Show("Permission denied."); }
            catch (OutOfMemoryException)
            { MessageBox.Show("Ran out of memmory."); }
            catch (IOException)
            { MessageBox.Show("I/O Exception"); }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
               "Created by Nick Bruno.\n" +
               "This utility recovers files from FAT file system.\n" +
               "GitHub: https://github.com/wholetthedogsoutside/ \n\n" +
               "This utility was made from the information I learned from Professor Avinash Srinivasan's "+
               "class.");
        }

        private void button_carve_Click(object sender, EventArgs e)
        {
            int recoverCount = fm.Carve();
            string message;
            switch (recoverCount)
            {
                case 0:
                    message = "No files have been recovered.";
                    break;
                case 1:
                    message = "One file has been recovered.";
                    saveToolStripMenuItem.Enabled = true;
                    break;
                default:
                    message = recoverCount + " files have been recovered.";
                    saveToolStripMenuItem.Enabled = true;
                    break;
            }
            MessageBox.Show(message);
        }
    }
}