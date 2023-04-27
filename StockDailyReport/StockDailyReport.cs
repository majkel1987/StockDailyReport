using Newtonsoft.Json;
using StockDailyReport.Cipher;
using StockDailyReport.Domains;
using StockDailyReport.EmailSender;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace StockDailyReport
{
    public partial class StockDailyReport : ServiceBase
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private int _sentHour;
        private int _intervalInMinutes;
        private bool _enableSendingReports;
        private Email _email;
        private string _emailReceiver;
        private string stockSymbol = ConfigurationManager.AppSettings["StockSymbol"];
        private const string NotEncryptedPassPrefix = "encrypt:";
        private StringCipher _stringCipher = new StringCipher("FE1D6E3A-4953-4D45-AB48-E5704DD0BA97");

        public StockDailyReport()
        {
            InitializeComponent();
            try
            {
                _emailReceiver = ConfigurationManager.AppSettings["ReceiverEmail"];
                _sentHour = Convert.ToInt32(ConfigurationManager.AppSettings["SentHour"]);
                _intervalInMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["IntervalInMinutes"]);
                _enableSendingReports = Convert.ToBoolean(ConfigurationManager.AppSettings["EnableSendingReports"]);

                _email = new Email(new EmailParams
                {
                    HostSmtp = ConfigurationManager.AppSettings["HostSmtp"],
                    Port = Convert.ToInt32(ConfigurationManager.AppSettings["Port"]),
                    EnableSsl = Convert.ToBoolean(ConfigurationManager.AppSettings["EnableSsl"]),
                    SenderName = ConfigurationManager.AppSettings["SenderName"],
                    SenderEmail = ConfigurationManager.AppSettings["SenderEmail"],
                    SenderEmailPassword = ConfigurationManager.AppSettings["SenderEmailPassword"]
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
                throw new Exception(ex.Message);
            }
        }
        private string DecryptSenderEmailPassword()
        {
            var encryptedEmailPassword = ConfigurationManager.AppSettings["SenderEmailPassword"];
            if (encryptedEmailPassword.StartsWith(NotEncryptedPassPrefix))
            {
                encryptedEmailPassword = _stringCipher.Encrypt(encryptedEmailPassword.Replace(NotEncryptedPassPrefix, ""));
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                configFile.AppSettings.Settings["SenderEmailPassword"].Value = encryptedEmailPassword;
                configFile.Save();
            }
            return _stringCipher.Decrypt(encryptedEmailPassword);
        }

        private string DecryptStockApiKey()
        {
            var encryptedApiKey = ConfigurationManager.AppSettings["StockApiKey"];
            if (encryptedApiKey.StartsWith(NotEncryptedPassPrefix))
            {
                encryptedApiKey = _stringCipher.Encrypt(encryptedApiKey.Replace(NotEncryptedPassPrefix, ""));
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                configFile.AppSettings.Settings["StockApiKey"].Value = encryptedApiKey;
                configFile.Save();
            }
            return _stringCipher.Decrypt(encryptedApiKey);
        }

        private string DecryptSmsToken()
        {
            var encryptedSmsToken = ConfigurationManager.AppSettings["SmsAccountToken"];
            if (encryptedSmsToken.StartsWith(NotEncryptedPassPrefix))
            {
                encryptedSmsToken = _stringCipher.Encrypt(encryptedSmsToken.Replace(NotEncryptedPassPrefix, ""));
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                configFile.AppSettings.Settings["SmsAccountToken"].Value = encryptedSmsToken;
                configFile.Save();
            }
            return _stringCipher.Decrypt(encryptedSmsToken);
        }

        protected override void OnStart(string[] args)
        {
            Timer _timer = new Timer(_intervalInMinutes * 60000);
            _timer.Elapsed += DoWork;
            _timer.Start();
            Logger.Info("Service start...");
        }

        public async Task<string> GetDataFromApi()
        {
            string message = "";
            string ApiKey = ConfigurationManager.AppSettings["StockApiKey"];
            string lastClosedTradingDay = DateTime.Now.ToString("yyyy-MM-dd");
            string url = $"http://api.marketstack.com/v1/eod/" +
                $"{lastClosedTradingDay}?access_key={ApiKey}&symbols={stockSymbol}";

            HttpClient httpClient = new HttpClient();
            try
            {
                var httpResponseMessage = await httpClient.GetAsync(url);
                string jsonResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                Root stockData = JsonConvert.DeserializeObject<Root>(jsonResponse);

                foreach (var item in stockData.Data)
                {
                    message = $"Raport dla spólki {item.Symbol} " +
                        $"z dnia {item.Date.ToString("dd-MM-yyyy")}: " +
                        $"kurs otwarcia {item.Open}$, kurs zamknięcia {item.Close}$.";
                }
                
                Logger.Info("Data loaded ...");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
                throw new Exception(ex.Message);
            }

            return message;
        }

        private async void DoWork(object sender, ElapsedEventArgs e)
        {
            try
            {
                await GetDataFromApi();
                await SendReport();
                await SendSMS();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
                throw new Exception(ex.Message);
            }
        }

        private async Task SendReport()
        {
            var actualHour = DateTime.Now.Hour;
            var message = await GetDataFromApi();
            

            if (_enableSendingReports)
            {
                if (actualHour < _sentHour)
                    return;

                if (message == null)
                    return;

                await _email.Send($"Raport dobowy dla spółki {stockSymbol}", message, _emailReceiver);

                Logger.Info("E-mail sent.");
            }
        }

        private async Task SendSMS() 
        {
            var accountSid = ConfigurationManager.AppSettings["SmsAccountSid"];
            var authToken = ConfigurationManager.AppSettings["SmsAccountToken"];
            var message = await GetDataFromApi();
            
            try
            {
                TwilioClient.Init(accountSid, authToken);

                var SMSmessage = MessageResource.Create(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(ConfigurationManager.AppSettings["TwilioNumber"]),
                    to: new Twilio.Types.PhoneNumber(ConfigurationManager.AppSettings["SmsRecieverNumber"])
                    );
                Logger.Info("SMS sent.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
                throw new Exception(ex.Message);
            }

        }

        protected override void OnStop()
        {
            Logger.Info("Service stopped...");
        }
    }
}
