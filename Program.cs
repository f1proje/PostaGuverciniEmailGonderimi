using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PostaGuverciniMailGonderme
{
    class Program
    {
        /* Bu uygulama postagüvercini.com üzerinde email atmaya yarar. Benim vaktim yandı sizinki yanmasın */

        private static readonly Encoding encoding = Encoding.UTF8;
        static void Main(string[] args)
        {
            string kullaniciAdi = "userName";
            string sifre = "Password";
            string konu = "Kayıt olduğunuz için teşekkürler";

            //MailContentType HTML olarak belirtildiği için, mailin içeğinde mutlaka HTML etiketi kullanmak gerekiyor.
            //Yoksa hata veriyor. Sağolsunlar hata mesajları da bir ayrıntılı bir ayrıntılı.
            string icerik = HttpUtility.HtmlEncode("<strong>Kayıt oldunuz</strong> teşekkürler<br/>");
            string alicilar = "ad.soyad@ggmail.com;ad.soyad@gggmail.com";

            //sadece pdf gönderdiğimiz için PostaGuverciniAttachEkle içinde application/pdf vs var. Eger foto vs gönderilecekse o kısmın güncellenmesi gerekir.
            List<string> dosyalar = new List<string>();
            dosyalar.Add(Environment.CurrentDirectory + "\\..\\..\\pdf\\sil.pdf");

            //Önce maili yarat
            string mailID = PostaGuverciniMailiOlustur("http://epostaapi.postaguvercini.com/CreateEMail.aspx", kullaniciAdi, sifre, konu, icerik);
            if (mailID == null) return;

            //attachment varsa attachment method unu çağır.
            if (dosyalar != null)
            {
                bool eklendiMi = PostaGuverciniAttachEkle("http://epostaapi.postaguvercini.com/AddAttachment.aspx", kullaniciAdi, sifre, mailID, dosyalar);
            }

            bool gonderildiMi = PostaGuverciniMailiniGonder("http://epostaapi.postaguvercini.com/SendEmail.aspx", kullaniciAdi, sifre, mailID, alicilar);

            //başarısız ise, diger posta gönderme yontemi ile dene...
        }
        static bool PostaGuverciniMailiniGonder(string url, string kullaniciAdi, string sifre, string mailID, string mailReceivers)
        {
            var dict = new Dictionary<string, object>();
            dict.Add("UserName", kullaniciAdi);
            dict.Add("Password", sifre);
            dict.Add("MailUID", mailID);
            dict.Add("MailReceivers", mailReceivers);

            HttpWebResponse response = MultipartFormPost(url, dict);
            using (Stream dataStream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();

                if (responseFromServer.Contains("200|"))
                {
                    return true;
                }
            }

            return false;
        }
        static string PostaGuverciniMailiOlustur(string url, string kullaniciAdi, string sifre, string konu, string icerik)
        {
            var dict = new Dictionary<string, object>();
            dict.Add("UserName", kullaniciAdi);
            dict.Add("Password", sifre);
            dict.Add("MailSubject", konu);
            dict.Add("MailContent", icerik);
            dict.Add("MailContentType", "HTML");

            HttpWebResponse response = MultipartFormPost(url, dict);
            using (Stream dataStream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();

                if (responseFromServer.Contains("200|"))
                {
                    return responseFromServer.Split(new string[] { "|" }, StringSplitOptions.None)[1];
                }
            }
            return null;
        }
        static bool PostaGuverciniAttachEkle(string url, string kullaniciAdi, string sifre, string mailID, List<string> ekDosyalar)
        {
            var dict = new Dictionary<string, object>();
            dict.Add("UserName", kullaniciAdi);
            dict.Add("Password", sifre);
            dict.Add("MailUid", mailID);

            for (int i = 0; i < ekDosyalar.Count; i++)
            {
                byte[] bytes = System.IO.File.ReadAllBytes(ekDosyalar[i]);
                dict.Add("file" + (i + 1).ToString(), new FileParameter(bytes, Path.GetFileName("file" + (i + 1).ToString()), "application/pdf"));
            }

            HttpWebResponse response = MultipartFormPost(url, dict);
            using (Stream dataStream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();

                if (responseFromServer.Contains("200|"))
                {
                    return true;
                }
            }

            return false;
        }
        static HttpWebResponse MultipartFormPost(string postUrl, Dictionary<string, object> postParameters)
        {
            string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;

            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            return PostForm(postUrl, contentType, formData);
        }
        private static HttpWebResponse PostForm(string postUrl, string contentType, byte[] formData)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null)
            {
                throw new NullReferenceException("request is not a http request");
            }

            request.Method = "POST";
            request.ContentType = contentType;
            //request.UserAgent = userAgent;
            request.CookieContainer = new CookieContainer();
            request.ContentLength = formData.Length;

            //request.Headers.Add(headerkey, headervalue);

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(formData, 0, formData.Length);
                requestStream.Close();
            }

            return request.GetResponse() as HttpWebResponse;
        }
        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {

                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter) // to check if parameter if of file type
                {
                    FileParameter fileToUpload = (FileParameter)param.Value;

                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                        boundary,
                        param.Key,
                        fileToUpload.FileName ?? param.Key,
                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));
                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                }
                else
                {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                        boundary,
                        param.Key,
                        param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }
        public class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }
        }
    }
}
