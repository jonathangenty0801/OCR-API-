using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using org.apache.pdfbox.pdmodel;
using org.apache.pdfbox.util;
using ImageMagick;
using System.Net;
using System.Drawing.Drawing2D;
using Newtonsoft.Json.Linq;
using System.Drawing.Imaging;
namespace OCRConsole
{
    public class Program
    {
        static void Main(string[] args)
        {

            string fileName = "01AGL01.pdf";
            //Console.WriteLine("Please input file path:");
            //fileName = Console.ReadLine();
            string base64Image = "";
            string[] base64=new string[0];
            var allData = string.Empty;
            PDDocument doc = null;
            string[] pattern_kind = get_patterntype();
            string[] pattern_type = get_patternkind();
            string[][] patterns = get_patterns(pattern_kind);
            if (pattern_kind == null || pattern_kind.Length == 0)
                return;
            //Check file is pdf 
            string extension = Path.GetExtension(fileName);
            bool isPdf = false;
            if (extension.ToLower() == ".pdf")
            {
                isPdf = true;
            }
            byte[] imageBytes = System.IO.File.ReadAllBytes( fileName);
            if (isPdf)
            {
                java.io.InputStream inputStream = new java.io.ByteArrayInputStream(imageBytes);
                doc = PDDocument.load(inputStream);
                PDFTextStripper stripper = new PDFTextStripper();
                allData = stripper.getText(doc);
                base64 = NonSearchablePDF(imageBytes);
                ////Check if PDF if Non-Searchable or searchable pdf
                //if (String.IsNullOrEmpty(allData) || allData.Length < 60)
                //{
                //    base64Image = NonSearchablePDF(imageBytes);
                //    flag = true;
                //}
                //else
                //{
                //    //Searchable PDF
                //    base64Image = Convert.ToBase64String(imageBytes); //CheckRotationAndConvertToBase64(imageBytes);
                //}
            }
            else
            {
                //base64String = Convert.ToBase64String(imageBytes); 
                base64Image = CheckRotationAndConvertToBase64(imageBytes);
                Array.Resize<string>(ref base64, base64.Length + 1);
                base64[base64.Length - 1] = base64Image;
            }
            string[] ocr_result = new string[pattern_type.Length];
            for (int i = 0; i < ocr_result.Length; i++)
                ocr_result[i] = "";
            for (int page=0;page<base64.Length;page++)
            {
                base64Image = base64[page];
                string rawData = RecognizeText(base64Image);
                string[] fulltext = parseJsontostring(rawData);
                
                for (int i = 0; i < fulltext.Length; i++)
                {
                    for (int k = 0; k < pattern_kind.Length; k++)
                    {
                        if (check_patterns(k, fulltext[i], patterns))
                        {
                            if (pattern_type[k] == "address")
                            {
                                if (fulltext[i].Length > 3 && ocr_result[k] == "")
                                    ocr_result[k] = boxes1(rawData, fulltext[i - 1], fulltext[i]);
                            }
                            else if (pattern_type[k] == "string" && ocr_result[k] == "")
                            {
                                ocr_result[k] = boxes(rawData, fulltext[i], fulltext);
                            }
                            else if (pattern_type[k] == "type" && ocr_result[k] == "")
                                ocr_result[k] = get_type(k, fulltext[i], patterns);
                            else if (pattern_type[k] == "amount" && ocr_result[k] == "")
                            {
                                string str = boxes(rawData, fulltext[i], fulltext);
                                string str1 = "";
                                for (int s = 0; s < str.Length; s++)
                                {
                                    string ss = str.Substring(s, 1);
                                    if (ss == " ")
                                        continue;
                                    if (ss == "$")
                                        str1 += ss;
                                    else
                                    {
                                        if (s > 0 && s < str.Length - 1)
                                        {
                                            if (ss == "." || ss == ",")
                                            {
                                                str1 += ss;
                                                continue;
                                            }
                                        }
                                        int n;
                                        bool isNumeric = int.TryParse(ss, out n);
                                        if (isNumeric)
                                            str1 += ss;
                                    }
                                }
                                ocr_result[k] = str1;
                            }
                            else
                            {
                                if (ocr_result[k] == "")
                                {
                                    ocr_result[k] = boxes(rawData, fulltext[i], fulltext);
                                }
                            }
                            bool f = false;
                            for (int check=0;check<ocr_result.Length;check++)
                            {
                                if(ocr_result[check]=="")
                                {
                                    f = true;
                                    break;
                                }
                            }
                            if(!f)
                            {
                                string output1 = "";
                                for (int ii = 0; ii < pattern_kind.Length; ii++)
                                    output1 += pattern_kind[ii] + ": " + ocr_result[ii] + "\n";
                                Console.WriteLine(output1);
                                Console.Read();
                                return;
                            }
                        }
                    }
                }
            }
            
            string output = "";
            for (int i = 0; i < pattern_kind.Length; i++)
                output += pattern_kind[i] + ": " + ocr_result[i] + "\n";
            Console.WriteLine(output);
            Console.Read();
        }
        private static bool check_patterns(int index, string str, string[][] patterns)
        {
            for(int i=0;i<patterns[index].Length;i++)
            {
                if (str.ToLower().Contains(patterns[index][i].ToLower()))
                    return true;
            }
            return false;
        }
        private static string get_type(int index, string str, string[][] patterns)
        {
            for (int i = 0; i < patterns[index].Length; i++)
            {
                if (str.ToLower().Contains(patterns[index][i].ToLower()))
                    return patterns[index][i];
            }
            return "";
        }
        private static string[] get_patterntype()
        {
            string[] pattern_type = new string[0];
            string[] lines =File.ReadAllLines(@"configuration.txt");
            foreach (string line in lines)
            {
                if(line.Contains(":"))
                {
                    Array.Resize<string>(ref pattern_type, pattern_type.Length + 1);
                    pattern_type[pattern_type.Length - 1] = line.Split(':')[0];
                }
            }
            return pattern_type;
        }
        private static string[] get_patternkind()
        {
            string[] pattern_kind = new string[0];
            string[] lines = File.ReadAllLines(@"configuration.txt");
            foreach (string line in lines)
            {
                if (line.Contains(":"))
                {
                    Array.Resize<string>(ref pattern_kind, pattern_kind.Length + 1);
                    pattern_kind[pattern_kind.Length - 1] = line.Split(':')[1];
                }
            }
            return pattern_kind;
        }
        private static string[][] get_patterns(string[] pattern_kind)
        {
            if (pattern_kind == null || pattern_kind.Length == 0)
                return null;
            string[][] patterns = new string[pattern_kind.Length][];
           
            for (int i = 0; i < pattern_kind.Length; i++)
            {
                bool flag = false;
                patterns[i] = new string[0];
                string[] lines = File.ReadAllLines(@"configuration.txt");
                foreach (string line in lines)
                {
                    if (line.Contains(":"))
                    {
                        string str= line.Split(':')[0];
                        if(str== pattern_kind[i])
                        {
                            flag = true;
                        }
                        else
                            flag = false;
                        continue;
                    }
                    else
                    {
                        if(flag)
                        {
                            Array.Resize<string>(ref patterns[i], patterns[i].Length + 1);
                            patterns[i][patterns[i].Length - 1] = line.Split('\n')[0];
                        }
                        
                    }
                }
            }
            return patterns;
        }
        public static bool check_pattern(string[] str_d, JArray eve, int index)
        {
            if (eve.Count < index + str_d.Length - 1)
                return false;
            for (int i = 0; i < str_d.Length; i++)
            {
                var description = eve[index + i]["description"];
                var text = description.Value<string>();
                if (str_d[i] != text)
                    return false;
            }
            return true;
        }
        public static string boxes(string json, string pattern,string[] fulltxt)
        {
            int[][] data = new int[4][];
            for (int n = 0; n < 4; n++)
                data[n] = new int[2];
            string[] str_d = pattern.Split(' ');
            JObject obj = JObject.Parse(json);
            var events = (JArray)obj["responses"];
            var eve = (JArray)events[0]["textAnnotations"];
            int num = 0, i = 0;
            bool flag = false;
            while (i < str_d.Length)
            {
                for (int j = 0; j < eve.Count - 1; j++)
                {
                    var description = eve[j]["description"];
                    var text = description.Value<string>();
                    var description1 = eve[j + 1]["description"];
                    var text1 = description1.Value<string>();
                    if (text == str_d[i])
                    {
                        if (flag == false)
                            flag = check_pattern(str_d, eve, j);
                        if (flag)
                        {
                            if (num == 0)
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[0]["x"].Value<int>();
                                var y0 = boxes[0]["y"].Value<int>();
                                data[0][0] = x0; data[0][1] = y0;
                                var x1 = boxes[3]["x"].Value<int>();
                                var y1 = boxes[3]["y"].Value<int>();
                                data[3][0] = x1; data[3][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d.Length)
                                    break;
                            }
                            else
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[1]["x"].Value<int>();
                                var y0 = boxes[1]["y"].Value<int>();
                                data[1][0] = x0; data[1][1] = y0;
                                var x1 = boxes[2]["x"].Value<int>();
                                var y1 = boxes[2]["y"].Value<int>();
                                data[2][0] = x1; data[2][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d.Length)
                                    break;
                            }
                        }

                    }
                }
                break;
            }
            if (data[0][0] == 0 && data[0][1] == 0)
                return "";
            string value = "";
            // search in width
            int index = -1;
            int M = 1000000;
            for (int j = 0; j < eve.Count - 1; j++)
            {
                var boxes = eve[j]["boundingPoly"]["vertices"];
                var x0 = boxes[0]["x"].Value<int>();
                var y0 = boxes[0]["y"].Value<int>();
                var x1 = boxes[1]["x"].Value<int>();
                var y1 = boxes[1]["y"].Value<int>();
                var x2 = boxes[2]["x"].Value<int>();
                var y2 = boxes[2]["y"].Value<int>();
                var x3 = boxes[3]["x"].Value<int>();
                var y3 = boxes[3]["y"].Value<int>();
                if (x0 <= data[1][0] || y3 <= data[1][1])
                    continue;
                if (M > Math.Abs(y0 - data[0][1]))
                {
                    index = j;
                    M = Math.Abs(y0 - data[0][1]);
                }
            }
            if (index != -1 && M<50)
            {
                value = find_boxline(index, fulltxt, json);
               // var description = eve[index]["description"];
               // value = description.Value<string>();
            }
            else
            {
                for (int j = 0; j < eve.Count - 1; j++)
                {
                    var boxes = eve[j]["boundingPoly"]["vertices"];
                    var x0 = boxes[0]["x"].Value<int>();
                    var y0 = boxes[0]["y"].Value<int>();
                    var x1 = boxes[1]["x"].Value<int>();
                    var y1 = boxes[1]["y"].Value<int>();
                    var x2 = boxes[2]["x"].Value<int>();
                    var y2 = boxes[2]["y"].Value<int>();
                    var x3 = boxes[3]["x"].Value<int>();
                    var y3 = boxes[3]["y"].Value<int>();
                    if (x0 >= data[0][0] - 10 && x1 <= data[1][0] - 10 && y0 >= data[3][1])
                    {
                        if (M > Math.Abs(y0 - data[3][1]))
                        {
                            index = j;
                            M = Math.Abs(y0 - data[0][1]);
                        }
                    }

                }
                if (index != -1 && M<150)
                {
                    value = find_boxline(index, fulltxt, json);
                   // var description = eve[index]["description"];
                    //value = description.Value<string>();
                }
            }
            return value;
        }
        public static string find_boxline(int index, string[] fulltxt, string json)
        {
            JObject obj = JObject.Parse(json);
            var events = (JArray)obj["responses"];
            var eve = (JArray)events[0]["textAnnotations"];
            var mboxes = eve[index]["boundingPoly"]["vertices"];
            var mx0 = mboxes[0]["x"].Value<int>();
            var my0 = mboxes[0]["y"].Value<int>();
            var mx1 = mboxes[1]["x"].Value<int>();
            var my1 = mboxes[1]["y"].Value<int>();
            var mx2 = mboxes[2]["x"].Value<int>();
            var my2 = mboxes[2]["y"].Value<int>();
            var mx3 = mboxes[3]["x"].Value<int>();
            var my3 = mboxes[3]["y"].Value<int>();
            for (int n = 0; n < fulltxt.Length; n++)
            {
                string pattern = fulltxt[n];
                string[] str_d = pattern.Split(' ');
                for (int k = 0; k < str_d.Length; k++)
                {
                    for (int j = 0; j < eve.Count - 1; j++)
                    {
                        var description = eve[j]["description"];
                        var text = description.Value<string>();
                        var description1 = eve[j + 1]["description"];
                        var text1 = description1.Value<string>();
                        if (text == str_d[k])
                        {
                            var boxes = eve[j]["boundingPoly"]["vertices"];
                            var x0 = boxes[0]["x"].Value<int>();
                            var y0 = boxes[0]["y"].Value<int>();
                            var x1 = boxes[1]["x"].Value<int>();
                            var y1 = boxes[1]["y"].Value<int>();
                            var x2 = boxes[2]["x"].Value<int>();
                            var y2 = boxes[2]["y"].Value<int>();
                            var x3 = boxes[3]["x"].Value<int>();
                            var y3 = boxes[3]["y"].Value<int>();
                            if (mx0 == x0 && my0 == y0 && mx1 == x1 && my1 == y1 && my2 == y2 && mx2 == x2 && mx3 == x3 && my3 == y3)
                                return fulltxt[n];
                        }
                    }
                }
            }
            return "";
        }
        public static string boxes2(string json, string pattern1, string[] pattern2)
        {
            int[][] data = new int[4][];
            int[][] data1 = new int[4][];
            for (int n = 0; n < 4; n++)
            { data[n] = new int[2]; data1[n] = new int[2]; }
            string[] str_d = pattern1.Split(' ');

            JObject obj = JObject.Parse(json);
            var events = (JArray)obj["responses"];
            var eve = (JArray)events[0]["textAnnotations"];
            int num = 0, i = 0;
            bool flag = false;
            while (i < str_d.Length)
            {
                for (int j = 0; j < eve.Count - 1; j++)
                {
                    var description = eve[j]["description"];
                    var text = description.Value<string>();
                    var description1 = eve[j + 1]["description"];
                    var text1 = description1.Value<string>();
                    if (text == str_d[i])
                    {
                        if (flag == false)
                            flag = check_pattern(str_d, eve, j);
                        if (flag)
                        {
                            if (num == 0)
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[0]["x"].Value<int>();
                                var y0 = boxes[0]["y"].Value<int>();
                                data[0][0] = x0; data[0][1] = y0;
                                var x1 = boxes[3]["x"].Value<int>();
                                var y1 = boxes[3]["y"].Value<int>();
                                data[3][0] = x1; data[3][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d.Length)
                                    break;
                            }
                            else
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[1]["x"].Value<int>();
                                var y0 = boxes[1]["y"].Value<int>();
                                data[1][0] = x0; data[1][1] = y0;
                                var x1 = boxes[2]["x"].Value<int>();
                                var y1 = boxes[3]["y"].Value<int>();
                                data[2][0] = x1; data[2][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d.Length)
                                    break;
                            }
                        }

                    }
                }
                break;
            }
            int index = -1, M = 100000;
            for (int k = 0; k < pattern2.Length; k++)
            {
                string[] str_d1 = pattern2[k].Split(' ');
                num = 0; i = 0;
                flag = false;
                while (i < str_d1.Length)
                {
                    for (int j = 0; j < eve.Count - 1; j++)
                    {
                        var description = eve[j]["description"];
                        var text = description.Value<string>();
                        var description1 = eve[j + 1]["description"];
                        var text1 = description1.Value<string>();
                        if (text == str_d1[i])
                        {
                            if (flag == false)
                                flag = check_pattern(str_d1, eve, j);
                            if (flag)
                            {
                                if (num == 0)
                                {
                                    var boxes = eve[j]["boundingPoly"]["vertices"];
                                    var x0 = boxes[0]["x"].Value<int>();
                                    var y0 = boxes[0]["y"].Value<int>();
                                    data1[0][0] = x0; data1[0][1] = y0;
                                    var x1 = boxes[3]["x"].Value<int>();
                                    var y1 = boxes[3]["y"].Value<int>();
                                    data1[3][0] = x1; data1[3][1] = y1;
                                    num++;
                                    i++;
                                    if (i >= str_d1.Length)
                                        break;
                                }
                                else
                                {
                                    var boxes = eve[j]["boundingPoly"]["vertices"];
                                    var x0 = boxes[1]["x"].Value<int>();
                                    var y0 = boxes[1]["y"].Value<int>();
                                    data1[1][0] = x0; data1[1][1] = y0;
                                    var x1 = boxes[2]["x"].Value<int>();
                                    var y1 = boxes[3]["y"].Value<int>();
                                    data1[2][0] = x1; data1[2][1] = y1;
                                    num++;
                                    i++;
                                    if (i >= str_d1.Length)
                                        break;
                                }
                            }

                        }
                    }
                    break;
                }
                if (data[1][0] <= data1[0][0] && Math.Abs(data[1][1] - data1[0][1]) < 20)
                {
                    if (M > Math.Abs(data[1][0] - data1[0][0]))
                    {
                        M = Math.Abs(data[1][0] - data1[0][0]);
                        index = k;
                    }
                }
            }
            if (index != -1)
            {
                return pattern2[index];
            }
            return "";
        }
        public static string boxes1(string json, string pattern1, string pattern2)
        {
            int[][] data = new int[4][];
            int[][] data1 = new int[4][];
            for (int n = 0; n < 4; n++)
            { data[n] = new int[2]; data1[n] = new int[2]; }
            string[] str_d = pattern1.Split(' ');
            string[] str_d1 = pattern2.Split(' ');
            JObject obj = JObject.Parse(json);
            var events = (JArray)obj["responses"];
            var eve = (JArray)events[0]["textAnnotations"];
            int num = 0, i = 0;
            bool flag = false;
            while (i < str_d.Length)
            {
                for (int j = 0; j < eve.Count - 1; j++)
                {
                    var description = eve[j]["description"];
                    var text = description.Value<string>();
                    var description1 = eve[j + 1]["description"];
                    var text1 = description1.Value<string>();
                    if (text == str_d[i])
                    {
                        if (flag == false)
                            flag = check_pattern(str_d, eve, j);
                        if (flag)
                        {
                            if (num == 0)
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[0]["x"].Value<int>();
                                var y0 = boxes[0]["y"].Value<int>();
                                data[0][0] = x0; data[0][1] = y0;
                                var x1 = boxes[3]["x"].Value<int>();
                                var y1 = boxes[3]["y"].Value<int>();
                                data[3][0] = x1; data[3][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d.Length)
                                    break;
                            }
                            else
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[1]["x"].Value<int>();
                                var y0 = boxes[1]["y"].Value<int>();
                                data[1][0] = x0; data[1][1] = y0;
                                var x1 = boxes[2]["x"].Value<int>();
                                var y1 = boxes[3]["y"].Value<int>();
                                data[2][0] = x1; data[2][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d.Length)
                                    break;
                            }
                        }

                    }
                }
                break;
            }
            num = 0; i = 0;
            flag = false;
            while (i < str_d1.Length)
            {
                for (int j = 0; j < eve.Count - 1; j++)
                {
                    var description = eve[j]["description"];
                    var text = description.Value<string>();
                    var description1 = eve[j + 1]["description"];
                    var text1 = description1.Value<string>();
                    if (text == str_d1[i])
                    {
                        if (flag == false)
                            flag = check_pattern(str_d1, eve, j);
                        if (flag)
                        {
                            if (num == 0)
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[0]["x"].Value<int>();
                                var y0 = boxes[0]["y"].Value<int>();
                                data1[0][0] = x0; data1[0][1] = y0;
                                var x1 = boxes[3]["x"].Value<int>();
                                var y1 = boxes[3]["y"].Value<int>();
                                data1[3][0] = x1; data1[3][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d1.Length)
                                    break;
                            }
                            else
                            {
                                var boxes = eve[j]["boundingPoly"]["vertices"];
                                var x0 = boxes[1]["x"].Value<int>();
                                var y0 = boxes[1]["y"].Value<int>();
                                data1[1][0] = x0; data1[1][1] = y0;
                                var x1 = boxes[2]["x"].Value<int>();
                                var y1 = boxes[3]["y"].Value<int>();
                                data1[2][0] = x1; data1[2][1] = y1;
                                num++;
                                i++;
                                if (i >= str_d1.Length)
                                    break;
                            }
                        }

                    }
                }
                break;
            }
            if (data[0][0] != 0 && data[0][1] != 0)
            {
                if (Math.Abs(data[1][0] - data1[0][0]) < 100)
                    return pattern1 + " " + pattern2;
            }
            return pattern2;
        }
        public static string[] parseJsontostring(string json)
        {
            string[] str = new string[0];
            JObject obj = JObject.Parse(json);
            var events = (JArray)obj["responses"];
            var eve = events[0]["textAnnotations"];
            var description = eve[0]["description"];
            var text = description.Value<string>();
            str = text.Split('\n');
            Array.Resize<string>(ref str, str.Length - 1);
            return str;
        }
        public static string RecognizeText(string base64Image)
        {
            string googleApiURL = "https://vision.googleapis.com/v1/images:annotate?key=AIzaSyDb4iy1uvFqxcVaHpo9NIKCQzP7dRmDE1Q";
            string URL = googleApiURL;
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(URL);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            string stringToRemove = "";
            var index = base64Image.IndexOf(",");
            if (index > 0)
            {
                stringToRemove = base64Image.Substring(0, index + 1);
            }

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = "{" +
                          "\"requests\":[" +
                            "{" +
                              "\"image\":{" +
                                 "\"content\":\"" + (!string.IsNullOrEmpty(stringToRemove) ? base64Image.Replace(stringToRemove, "") : base64Image) + "\"" +
                              "}," +
                              "\"features\":[" +
                                "{" +
                                  "\"type\":\"TEXT_DETECTION\"," +
                                  "\"maxResults\":200" +
                                "}" +
                              "]" +
                            "}" +
                          "]" +
                        "}";

                streamWriter.Write(json);
            }

            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream());
            var jsonResult = streamReader.ReadToEnd();

            return jsonResult;
        }
        static string ConverImageToBase64(string imagePath)
        {
            string base64String = "";
            using (Image image = Image.FromFile(imagePath))
            {
                using (MemoryStream m = new MemoryStream())
                {
                    image.Save(m, image.RawFormat);
                    byte[] imageBytes = m.ToArray();
                    // Convert byte[] to Base64 String
                    base64String = Convert.ToBase64String(imageBytes);
                    //return base64String;
                }
            }
            return base64String;
        }


        private static string[] NonSearchablePDF(byte[] imageBytes)
        {
            string[] output = new string[0];
           
            List<Bitmap> imageOri1 = Pdf2OneImage(imageBytes);
            for (int k = 0; k < imageOri1.Count; k++)
            {
                string base64String = string.Empty;
                Bitmap imageOri = imageOri1[k];
                double rate = 1.0;
                if (imageOri.Width < imageOri.Height)
                {
                    if (imageOri.Width > 1500) rate = imageOri.Width / 1500.0;
                }
                else
                {
                    if (imageOri.Height > 1500) rate = imageOri.Height / 1500.0;
                }
                rate = 1.0;
                Image image = ResizeImage(imageOri, (int)(imageOri.Width / rate), (int)(imageOri.Height / rate));

                int width = image.Width;
                int height = image.Height;
                //     image.Save("D:\\a.jpg");
                using (MemoryStream m = new MemoryStream())
                {
                    image.Save(m, System.Drawing.Imaging.ImageFormat.Jpeg);
                    byte[] imgBytes = m.ToArray();

                    // Convert byte[] to Base64 String
                    //base64String = Convert.ToBase64String(imgBytes);
                    base64String = CheckRotationAndConvertToBase64(imgBytes);

                }
                Array.Resize<string>(ref output, output.Length + 1);
                output[output.Length - 1] = base64String;
            }
            return output;
        }

        public static List<Bitmap> Pdf2OneImage(byte[] imageBytes)
        {
            //   var d = Gosh( pdfFile);

            List<Bitmap> lstBmp = ConvertPdf2ListBitmap(imageBytes);
            return lstBmp;
        }

        private static List<Bitmap> ConvertPdf2ListBitmap(byte[] imageBytes)
        {
            MagickReadSettings settings = new MagickReadSettings();
            // Settings the density to 300 dpi will create an image with a better quality
            settings.Density = new Density(300);
            List<Bitmap> lResult = new List<Bitmap>();
            using (MagickImageCollection images = new MagickImageCollection())
            {
                try
                {
                    //var pathI = "D:\\Receipts\\Airtel Bill.pdf";
                    //images.Read(pathI, settings);
                    images.Read(imageBytes, settings);

                    int page = 1;
                    foreach (MagickImage image in images)
                    {
                        image.Format = MagickFormat.Jpg;
                        lResult.Add(image.ToBitmap());
                        page++;
                    }
                }
                catch (Exception e)
                {

                }// Add all the pages of the pdf file to the collection

            }

            return lResult;
        }

        private static Image ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return (Image)destImage;
        }

        private static string CheckRotationAndConvertToBase64(byte[] imageBytes)
        {
            string base64String = "";
            System.Drawing.Image image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(imageBytes));
            //if (image.PropertyIdList.Contains(0x0112)) //0x0112 is available in photoshop images
            //{
            //    PropertyItem propOrientation = image.GetPropertyItem(0x0112);
            //    short orientation = BitConverter.ToInt16(propOrientation.Value, 0);
            //    if (orientation == 6)
            //    {
            //        image.RotateFlip(RotateFlipType.Rotate90FlipNone);
            //    }
            //    else if (orientation == 8)
            //    {
            //        image.RotateFlip(RotateFlipType.Rotate270FlipNone);
            //    }
            //    else
            //    {
            //        // Do nothing
            //    }
            //}

            if (Array.IndexOf(image.PropertyIdList, 274) > -1)
            {
                var orientation = (int)image.GetPropertyItem(274).Value[0];
                switch (orientation)
                {
                    case 1:
                        // No rotation required.
                        break;
                    case 2:
                        image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        break;
                    case 3:
                        image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        break;
                    case 4:
                        image.RotateFlip(RotateFlipType.Rotate180FlipX);
                        break;
                    case 5:
                        image.RotateFlip(RotateFlipType.Rotate90FlipX);
                        break;
                    case 6:
                        image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        break;
                    case 7:
                        image.RotateFlip(RotateFlipType.Rotate270FlipX);
                        break;
                    case 8:
                        image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        break;
                }
                // This EXIF data is now invalid and should be removed.
                image.RemovePropertyItem(274);
            }
            //Convert to Base64
            using (MemoryStream m = new MemoryStream())
            {
                image.Save(m, System.Drawing.Imaging.ImageFormat.Jpeg);
                byte[] imageBytesArray = m.ToArray();

                // Convert byte[] to Base64 String
                base64String = Convert.ToBase64String(imageBytesArray);
            }

            return base64String;
        }
    }
}
