//// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class ITstudent
{
    public string Name { get; set; }
    public string ID { get; set; }
    public string Programme { get; set; }
    public Dictionary<string, double> Courses { get; set; }

    public ITstudent()
    {
        Courses = new Dictionary<string, double>();
    }
}

public class Program
{
    //static Queue<string> buffer = new Queue<string>();
    static Queue<Tuple<string, int>> buffer = new Queue<Tuple<string, int>>();
    static int bufferSize = 10;
    static int fileCount = 0;
    static object lockObj = new object();
    static bool allProduced = false;
    static object consumerLockObj = new object(); // Separate lock for signaling the consumer
    static ManualResetEvent producerFinishedEvent = new ManualResetEvent(false);



    // ... (WriteToFile, ReadFromFile, CalculateAverageMark, DeterminePassOrFail, DeleteFile)

    static void WriteToFile(string fileName, string data)
    {
        using (StreamWriter writer = new StreamWriter(fileName))
        {
            writer.Write(data);
        }
    }

    static string ReadFromFile(string fileName)
    {
        using (StreamReader reader = new StreamReader(fileName))
        {
            return reader.ReadToEnd();
        }
    }

    static double CalculateAverageMark(ITstudent student)
    {
        double totalMarks = 0;
        int numCourses = student.Courses.Count;

        foreach (double mark in student.Courses.Values)
        {
            totalMarks += mark;
        }

        return totalMarks / numCourses;
    }

    static bool DeterminePassOrFail(double averageMark)
    {
        return averageMark >= 50;
    }

    static void DeleteFile(string fileName)
    {
        File.Delete(fileName);
    }


    static void Main(string[] args)
    {
        Thread producerThread = new Thread(Producer);
        Thread consumerThread = new Thread(Consumer);

        producerThread.Start();
        consumerThread.Start();

        producerThread.Join();
        consumerThread.Join();
    }

    // Define a separate event for the producer to signal the consumer when a new item is produced
    static AutoResetEvent newItemProducedEvent = new AutoResetEvent(false);


    //....(Produces 1 and then send a signal to the Consumer to start so on
    static void Producer()
    {
        string sharedFolderPath = Path.Combine(Environment.CurrentDirectory, "shared");
        if (!Directory.Exists(sharedFolderPath))
        {
            Directory.CreateDirectory(sharedFolderPath);
        }

        while (true)
        {
            lock (lockObj)
            {
                if (buffer.Count < bufferSize)
                {
                    string fileName = "student" + (++fileCount) + ".xml";
                    ITstudent student = GenerateStudent();
                    string xmlData = WrapInXml(student);

                    // Use socket programming to send the XML data to the consumer
                    using (TcpClient client = new TcpClient("localhost", 12346))
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] data = Encoding.UTF8.GetBytes(xmlData);
                        stream.Write(data, 0, data.Length);
                    }

                    string filePath = Path.Combine(sharedFolderPath, fileName);
                    WriteToFile(filePath, xmlData);


                    // Add the corresponding integer (file number) to the buffer
                    buffer.Enqueue(new Tuple<string, int>(filePath, fileCount));
                    Console.WriteLine("Produced: " + Path.GetFileName(filePath));

                }
            }
        }
    }

    ////....(Produces all 10 and then send a signal to the Consumer to start
    //static void Producer()
    //{
    //    string sharedFolderPath = Path.Combine(Environment.CurrentDirectory, "shared");
    //    if (!Directory.Exists(sharedFolderPath))
    //    {
    //        Directory.CreateDirectory(sharedFolderPath);
    //    }

    //    // Start TCP server to listen for connections from the consumer
    //    TcpListener server = new TcpListener(IPAddress.Any, 12345);
    //    server.Start();
    //    Console.WriteLine("Producer is listening for connections...");

    //    while (true)
    //    {
    //        lock (lockObj)
    //        {
    //            if (!allProduced)
    //            {
    //                while (buffer.Count < bufferSize)
    //                {
    //                    string fileName = "student" + (++fileCount) + ".xml";
    //                    ITstudent student = GenerateStudent();
    //                    string xmlData = WrapInXml(student);
    //                    string filePath = Path.Combine(sharedFolderPath, fileName);
    //                    WriteToFile(filePath, xmlData);

    //                    // Add the corresponding integer (file number) to the buffer
    //                    buffer.Enqueue(new Tuple<string, int>(filePath, fileCount));
    //                    Console.WriteLine("Produced: " + Path.GetFileName(filePath));

    //                    if (fileCount >= 10)
    //                    {
    //                        allProduced = true;
    //                        Monitor.Pulse(lockObj); // Signal that the producer has finished
    //                        break;
    //                    }
    //                    // Signal the consumer that a new item has been produced
    //                    newItemProducedEvent.Set();
    //                }
    //            }
    //        }
    //    }
    //}

    //... (The Consumer, GenerateStudent, WrapInXml, UnwrapXml)
    //....(Comumes 1 and wait for producer so on
    static void Consumer()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 12346);
        listener.Start();

        while (true)
        {
            lock (lockObj)
            {
                if (buffer.Count > 0)
                {
                    //string fileName = buffer.Dequeue();
                    var fileTuple = buffer.Dequeue();
                    string fileName = fileTuple.Item1;
                    int fileNumber = fileTuple.Item2;

                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            int bytesRead;
                            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                memoryStream.Write(buffer, 0, bytesRead);
                            }

                            byte[] fileBytes = memoryStream.ToArray();
                            File.WriteAllBytes(fileName, fileBytes);
                        }

                        string xmlData = ReadFromFile(fileName);
                        ITstudent student = UnwrapXml(xmlData);
                        double averageMark = CalculateAverageMark(student);
                        bool passed = DeterminePassOrFail(averageMark);

                        Console.WriteLine("Student Name: " + student.Name);
                        Console.WriteLine("Student ID: " + student.ID);
                        Console.WriteLine("Programme: " + student.Programme);

                        Console.WriteLine("Courses and their associated marks:");
                        foreach (KeyValuePair<string, double> course in student.Courses)
                        {
                            Console.WriteLine(course.Key + ": " + course.Value);
                        }

                        Console.WriteLine("Average: " + averageMark);
                        Console.WriteLine("Pass/Fail: " + (passed ? "Pass" : "Fail") + " \n");

                        DeleteFile(fileName);
                    }
                }
            }
        }
    }

    ////....(Comsumes all 10 produced
    //static void Consumer()
    //     {
    //    lock (lockObj) // Acquire the lock before entering the consumer loop
    //    {
    //        while (true)
    //        {
    //            if (!allProduced && buffer.Count == 0)
    //            {
    //                // Wait for the producer to produce items
    //                Monitor.Wait(lockObj);
    //            }

    //            if (buffer.Count > 0)
    //            {

    //                var fileTuple = Program.buffer.Dequeue();
    //                string fileName = fileTuple.Item1;
    //                int fileNumber = fileTuple.Item2;

    //                // Connect to the producer's server (assumed to be running on localhost, port 12345)
    //                TcpClient client = new TcpClient();
    //                client.Connect("127.0.0.1", 12345);

    //                NetworkStream stream = client.GetStream();

    //                // Send the file number to the producer to request the corresponding XML data
    //                byte[] requestBytes = Encoding.ASCII.GetBytes(fileNumber.ToString());
    //                stream.Write(requestBytes, 0, requestBytes.Length);

    //                // Receive the XML data from the producer
    //                byte[] buffer = new byte[1024];
    //                int bytesRead = stream.Read(buffer, 0, buffer.Length);
    //                string xmlData = Encoding.ASCII.GetString(buffer, 0, bytesRead);

    //                // Close the connection to the producer
    //                client.Close();


    //                //string fileName = buffer.Dequeue();
    //                //string xmlData = ReadFromFile(fileName);
    //                ITstudent student = UnwrapXml(xmlData);
    //                double averageMark = CalculateAverageMark(student);
    //                bool passed = DeterminePassOrFail(averageMark);

    //                Console.WriteLine("Student Name: " + student.Name);
    //                Console.WriteLine("Student ID: " + student.ID);
    //                Console.WriteLine("Programme: " + student.Programme);

    //                Console.WriteLine("Courses and their associated marks:");
    //                foreach (KeyValuePair<string, double> course in student.Courses)
    //                {
    //                    Console.WriteLine(course.Key + ": " + course.Value);
    //                }

    //                Console.WriteLine("Average: " + averageMark);
    //                Console.WriteLine("Pass/Fail: " + (passed ? "Pass" : "Fail") + " \n");

    //                DeleteFile(fileName);
    //            }

    //            if (allProduced && buffer.Count == 0)
    //            {
    //                // Exit the consumer loop when all items have been produced and consumed
    //                break;
    //            }
    //        }
    //    }
    //}

    static ITstudent GenerateStudent()
    {
        Random random = new Random();
        ITstudent student = new ITstudent();

        student.Name = "Student" + random.Next(1000, 9999);
        student.ID = random.Next(10000000, 99999999).ToString();
        student.Programme = "BSc. Information Technology";

        // Define an array of course names
        string[] courseNames = { "Entrepreneurship and Innovation", "Intergrative Programming and Technologies", "Security I", "System Admin and Maintenance", "Descrete Mathematics" };

        int numCourses = random.Next(1, 6);
        for (int i = 0; i < numCourses; i++)
        {
            // Get the course name from the array using the index i
            string courseName = courseNames[i];
            double mark = random.Next(0, 101);
            student.Courses.Add(courseName, mark);
        }

        return student;
    }

    static string WrapInXml(ITstudent student)
    {
        string xmlData =
            "<Student>" +
                "<Name>" + student.Name + "</Name>" +
                "<ID>" + student.ID + "</ID>" +
                "<Programme>" + student.Programme + "</Programme>" +
                "<Courses>";

        foreach (KeyValuePair<string, double> course in student.Courses)
        {
            xmlData += "<Course>" +
                            "<Name>" + course.Key + "</Name>" +
                            "<Mark>" + course.Value.ToString() + "</Mark>" +
                      "</Course>";
        }

        xmlData += "</Courses></Student>";

        return xmlData;
    }

    static ITstudent UnwrapXml(string xmlData)
    {
        ITstudent student = new ITstudent();

        int startIndex, endIndex;

        startIndex = xmlData.IndexOf("<Name>") + 6;
        endIndex = xmlData.IndexOf("</Name>");
        student.Name = xmlData.Substring(startIndex, endIndex - startIndex);

        startIndex = xmlData.IndexOf("<ID>") + 4;
        endIndex = xmlData.IndexOf("</ID>");
        student.ID = xmlData.Substring(startIndex, endIndex - startIndex);

        startIndex = xmlData.IndexOf("<Programme>") + 11;
        endIndex = xmlData.IndexOf("</Programme>");
        student.Programme = xmlData.Substring(startIndex, endIndex - startIndex);

        startIndex = xmlData.IndexOf("<Course>") + 8;
        while (startIndex != -1)
        {
            endIndex = xmlData.IndexOf("</Course>", startIndex);

            int nameStartIndex, nameEndIndex, markStartIndex, markEndIndex;

            nameStartIndex = xmlData.IndexOf("<Name>", startIndex) + 6;
            nameEndIndex = xmlData.IndexOf("</Name>", startIndex);

            markStartIndex = xmlData.IndexOf("<Mark>", startIndex) + 6;
            markEndIndex = xmlData.IndexOf("</Mark>", startIndex);

            string courseName =
                xmlData.Substring(nameStartIndex, nameEndIndex - nameStartIndex);

            double courseMark =
                double.Parse(xmlData.Substring(markStartIndex, markEndIndex - markStartIndex));

            student.Courses.Add(courseName, courseMark);

            startIndex = xmlData.IndexOf("<Course>", endIndex);
        }

        return student;
    }

}

