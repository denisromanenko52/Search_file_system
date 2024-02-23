using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Search_file_system
{
    public partial class Form1 : Form
    {
        private TreeNode rootNode;
        private string startDirectory;
        private Regex filePattern;
        private bool isSearching;
        private int foundFilesCount = 0;
        private int totalFilesCount = 0;
        private bool stopSearch;
        private CancellationTokenSource cancellationTokenSource;
        BackgroundWorker worker = new BackgroundWorker();
        private DateTime startTime;
        private List<string> pausedFiles;
        private Stack<string> directoriesStack;
        private bool isPaused = false;
        private DateTime pauseTime;
        
        public Form1()
        {
            InitializeComponent();
            InitializeTreeView();
            InitializeSearch();
        }

        private void InitializeTreeView()
        {
            treeView1.Nodes.Clear();
            rootNode = treeView1.Nodes.Add("Files");
        }

        private void InitializeSearch()
        {
            startDirectory = "";
            filePattern = new Regex("");
            isSearching = false;
            foundFilesCount = 0;
            totalFilesCount = 0;
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (worker != null && worker.IsBusy)
            {
                cancellationTokenSource.Cancel();
            }

            startDirectory = textBox1.Text;
            filePattern = new Regex(textBox2.Text);

            worker.WorkerSupportsCancellation = true;

            if (button1.Text == "Пауза")
            {
                timer1.Stop();
                
                pauseTime = DateTime.Now;
                
                button1.Text = "Продолжить";
                
                pausedFiles = new List<string>(cancellationTokenSource.Token.IsCancellationRequested ? new List<string>() : Directory.GetFiles(startDirectory).ToList());
                
                directoriesStack = new Stack<string>();

                var node = treeView1.Nodes[0];
                
                foreach (TreeNode subNode in node.Nodes)
                {
                    directoriesStack.Push(Path.Combine(startDirectory, subNode.Text));
                }

                isPaused = true;

                cancellationTokenSource.Cancel();
            }
            else if (button1.Text == "Продолжить")
            {
                startTime = startTime.Add(DateTime.Now - pauseTime);
                
                timer1.Start();
                
                button1.Text = "Пауза";
                
                cancellationTokenSource = new CancellationTokenSource();
                
                worker.DoWork += (obj, ea) => ResumeSearch(cancellationTokenSource.Token);
                worker.RunWorkerAsync();
            }
            else if (button1.Text == "Поиск")
            {
                if (treeView1.Nodes[0].Nodes.Count > 0)
                {
                    InitializeTreeView();
                }

                label1.Text = "00:00:00:00";
                
                startTime = DateTime.Now;
                
                timer1.Start();
                
                button1.Text = "Пауза";
                
                totalFilesCount = 0;
                foundFilesCount = 0;

                worker.DoWork += (obj, ea) => SearchFiles(startDirectory, filePattern, rootNode, cancellationTokenSource.Token);
                worker.RunWorkerAsync();
            }
        }

        private void UpdateCurrentDirectoryLabel(string directory)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate {
                    label3.Text = $"Current Directory: {directory}";
                });
            }
            else
            {
                label3.Text = $"Current Directory: {directory}";
            }
        }

        private void SearchFiles(string directory, Regex pattern, TreeNode parentNode, CancellationToken cancellationToken)
        {
            try
            {
                treeView1.SuspendLayout();

                cancellationToken.ThrowIfCancellationRequested();

                UpdateCurrentDirectoryLabel(directory);

                List<string> files = Directory.GetFiles(directory).Where(file => pattern.IsMatch(Path.GetFileName(file))).ToList();

                totalFilesCount += files.Count;

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    TreeNode node = new TreeNode(Path.GetFileName(file));
                    
                    AddNodeToParent(parentNode, node);

                    Interlocked.Increment(ref foundFilesCount);

                    UpdateFileCountLabels();

                    Thread.Sleep(100);
                }

                List<string> subdirectories = Directory.GetDirectories(directory).ToList();

                foreach (var subdirectory in subdirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    TreeNode node = new TreeNode(Path.GetFileName(subdirectory));
                    
                    AddNodeToParent(parentNode, node);
                    
                    Thread.Sleep(100);
                    
                    SearchFiles(subdirectory, pattern, node, cancellationToken);
                    
                    Thread.Sleep(100);
                }

                treeView1.ResumeLayout();

                timer1.Stop();

                button1.Text = "Поиск";
            }
            catch (OperationCanceledException)
            {
                
            }
            catch (UnauthorizedAccessException)
            {
                
            }
        }

        private void UpdateFileCountLabels()
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate {
                    label2.Text = $"Found files: {foundFilesCount} Total files: {totalFilesCount}";
                });
            }
            else
            {
                label2.Text = $"Found files: {foundFilesCount} Total files: {totalFilesCount}";
            }
        }

        private void ResumeSearch(CancellationToken cancellationToken)
        {
            treeView1.SuspendLayout();

            try
            {
                while (directoriesStack.Count != 0)
                {
                    var directory = directoriesStack.Peek();
                    TreeNode parentNode = new TreeNode(Path.GetFileName(directory));
                    AddNodeToParent(rootNode, parentNode);
                  
                    List<string> files = pausedFiles.Where(file => Path.GetDirectoryName(file) == directory).ToList();

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        TreeNode node = new TreeNode(Path.GetFileName(file));

                        UpdateCurrentDirectoryLabel(directory);

                        AddNodeToParent(parentNode, node);
                     
                        Thread.Sleep(100);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    List<string> subdirectories = Directory.GetDirectories(directory).ToList();

                    foreach (var subdirectory in subdirectories)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        TreeNode node = new TreeNode(Path.GetFileName(subdirectory));
                        
                        AddNodeToParent(parentNode, node);
                        
                        Thread.Sleep(100);

                        directoriesStack.Push(subdirectory);
                    }

                    directoriesStack.Pop();

                    Thread.Sleep(100);
                }
            }
            catch (OperationCanceledException)
            {
               
            }
            finally
            {
                treeView1.ResumeLayout();

                timer1.Stop();

                button1.Text = "Поиск";
            }
        }

        private void AddNodeToParent(TreeNode parent, TreeNode node)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate { parent.Nodes.Add(node); });
            }
            else
            {
                parent.Nodes.Add(node);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            TimeSpan elapsedTime = DateTime.Now - startTime;
            label1.Text = elapsedTime.ToString(@"hh\:mm\:ss\:ff");
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (worker != null && worker.IsBusy)
            {
                cancellationTokenSource.Cancel();
            }

            InitializeTreeView();

            timer1.Stop();
            
            label1.Text = "00:00:00:00";

            InitializeSearch();

            button1.Text = "Поиск";
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Properties.Settings.Default.StartDirectory = textBox1.Text;
            //Properties.Settings.Default.FileNamePattern = textBox2.Text;
            //Properties.Settings.Default.Save();

            using (StreamWriter writer = new StreamWriter("saved_text.txt"))
            {
                writer.WriteLine(textBox1.Text);
                writer.WriteLine(textBox2.Text);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //textBox1.Text = Properties.Settings.Default.StartDirectory;
            //textBox2.Text = Properties.Settings.Default.FileNamePattern;

            if (File.Exists("saved_text.txt"))
            {
                using (StreamReader reader = new StreamReader("saved_text.txt"))
                {
                    textBox1.Text = reader.ReadLine();
                    textBox2.Text = reader.ReadLine();
                }
            }
        }
    }
}
