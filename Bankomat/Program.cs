using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;

namespace Bankomat
{
    class Program
    {
        class Konta
        {
            public string _id { get; set; }
            public double balance { get; set; }
            public int tries { get; set; }
            public string pin { get; set; }
            public string[] history { get; set; }
            public Payer[] payers { get; set; }

        }
        class Payer
        {
            public string account { get; set; }
            public string nick { get; set; }
        }

        class BankomatStatus
        {
            private ulong twohundred, hundred, fifty, twenty;

            public BankomatStatus(ulong twohundred, ulong hundred, ulong fifty, ulong twenty)
            {
                this.twohundred += twohundred;
                this.hundred += hundred;
                this.fifty += fifty;
                this.twenty += twenty;
            }
            public void AddMoney(ulong twohundred, ulong hundred, ulong fifty, ulong twenty) 
            {
                this.twohundred += twohundred;
                this.hundred += hundred;
                this.fifty += fifty;
                this.twenty += twenty;
                Console.WriteLine("Uzupełniono bankomat: " + twohundred + " * {200zł}, " + hundred + " * {100zł}, " + fifty + " * {50zł}, " + twenty + " * {20zł}");
                File.AppendAllText("log.txt", DateTime.Now.ToString() + " Uzupełniono bankomat: " + twohundred + " * {200zł}, " + hundred + " * {100zł}, " + fifty + " * {50zł}, " + twenty + " * {20zł}" + Environment.NewLine);
                
            }
            public bool Withdraw(double pricetemp, string account)
            {
                ulong twohundredtemp = 0, hundredtemp = 0, fiftytemp = 0, twentytemp = 0, price = (ulong)pricetemp;

                if (price % 50 == 0 || price % 50 == 20 || price % 50 == 40 || price % 50 == 10 || price % 50 == 30 && price != 10 && price != 30)
                {

                    if (price >= 200)
                    {
                        if(price % 50 == 10 || price % 50 == 30)
                            twohundredtemp = (price / 200) - 1 <= twohundred ? (price / 200) - 1 : twohundred;
                        else
                            twohundredtemp = price / 200 <= twohundred ? price / 200 : twohundred;
                        price -= twohundredtemp * 200;
                    }
                    if (price >= 100)
                    {
                        if(price % 50 == 10 || price % 50 == 30)
                            hundredtemp = (price / 100) - 1 <= hundred ? (price / 100) - 1 : hundred;
                        else
                            hundredtemp = price / 100 <= hundred ? price / 100 : hundred;
                        price -= hundredtemp * 100;
                    }

                    if (price >= 50)
                    {
                        if(price % 50 == 10 || price % 50 == 30)
                            fiftytemp = (price / 50) -1 <= fifty ? (price / 50) - 1 : fifty;
                        else
                            fiftytemp = price / 50 <= fifty ? price / 50 : fifty;
                        price -= fiftytemp * 50;
                    }
                    if (price >= 20 && price / 20 <= twenty)
                    {
                        twentytemp = price / 20;
                        price -= twentytemp * 20;
                    }

                    if (price != 0)
                    {
                        Console.WriteLine("Brak banknotów aby wypłacić środki");
                        return false;
                    }
                    if (BankManager.Withdraw(account, pricetemp))
                    {
                        Console.WriteLine("wypłacono " + pricetemp + " zł, " + twohundredtemp + " * {200zł}, " + hundredtemp + " * {100zł}, " + fiftytemp + " * {50zł}, " + twentytemp + " * {20zł}");
                        File.AppendAllText("log.txt", DateTime.Now.ToString() + " wypłata " + pricetemp + " zł, " + twohundredtemp + " * {200zł}, " + hundredtemp + " * {100zł}, " + fiftytemp + " * {50zł}, " + twentytemp + " * {20zł}" + Environment.NewLine);
                        
                        twohundred -= twohundredtemp;
                        hundred -= hundredtemp;
                        fifty -= fiftytemp;
                        twenty -= twentytemp;
                        return true;
                    }
                    else return false;

                }
                else
                {
                    Console.WriteLine("Wprowadzono złą kwotę");
                    return false;
                }

            }

            public void Status()
            {
                ulong suma = twohundred * 200 + hundred * 100 + fifty * 50 + twenty * 20;
                Console.WriteLine("Stan bankomatu: "+ suma +" zł. {200zł}: " + twohundred + ", {100zł}: " + hundred + ", {50zł}: " + fifty + ", {20zł}: " + twenty);
            }
            public void BankomatLog()
            {
                foreach(string line in File.ReadAllLines("log.txt"))
                {
                    Console.WriteLine(line);
                }
            }
            
        }


        class BankManager
        {
            private static MongoClient client = new MongoClient("mongodb://user:user@bank-shard-00-00-5ikhi.azure.mongodb.net:27017,bank-shard-00-01-5ikhi.azure.mongodb.net:27017,bank-shard-00-02-5ikhi.azure.mongodb.net:27017/test?ssl=true&replicaSet=bank-shard-0&authSource=admin&retryWrites=true&w=majority");
            private static IMongoDatabase db = client.GetDatabase("bank");
            private static IMongoCollection<Konta> coll = db.GetCollection<Konta>("konta");
            public enum Logincodes { ok = 0, wrongLogin = 1, wrongPin = 2, blocked = 3 }
            public enum Errorcodes { ok = 0, noEnoughMoney = 1, wrongPrice = 2 , wrongAccount = 3, itsYourAccount = 4, toBigPrice = 5, accountExist = 6 }

            public static int Login(string account, string pin)
            {
                List<Konta> users = coll.Find(b => b._id == account).ToList();
                if (users.Count == 1)
                {
                    if (users[0].pin == pin && users[0].tries < 3)
                    {
                        return (int)Logincodes.ok;
                    }
                    else if (users[0].pin == pin && users[0].tries >= 3)
                    {
                        return (int)Logincodes.blocked;
                    }
                    else
                    {
                        var filter = Builders<Konta>.Filter.Eq("_id", account);
                        var update = Builders<Konta>.Update.Set("tries", users[0].tries + 1);
                        coll.UpdateOne(filter, update);
                        return (int)Logincodes.wrongPin;
                    }
                }
                else
                    return (int)Logincodes.wrongLogin;
            }
            public static bool Withdraw(string account, double value)
            {
                List <Konta> user = coll.Find(b => b._id == account).ToList();
                if (user[0].balance >= value && value > 0)
                {
                    var filter = Builders<Konta>.Filter.Eq("_id", account);
                    var update = Builders<Konta>.Update.Set("balance", user[0].balance - value);
                    coll.UpdateOne(filter, update);
                    AddHistory(account, "wypłacono", value);
                    Console.WriteLine("Wypłacono " + value + "zł, obecny stan konta to:" + GetBalance(account) + "zł");
                    return true;
                }
                else  
                {
                    Console.WriteLine("Niewystarczająca ilość środków na koncie");
                    return false;
                }


            }
            public static int Transfer(string account, double value, string transferAccount)
            {
                List<Konta> user = coll.Find(b => b._id == account).ToList();
                List<Konta> transfer = coll.Find(b => b._id == transferAccount).ToList();
                if (transfer.Count == 0)
                {
                    return (int)Errorcodes.wrongAccount;
                }
                if (account == transferAccount)
                {
                    return (int)Errorcodes.itsYourAccount;
                }
                if (user[0].balance >= value && value > 0)
                {
                    if (transfer[0].balance + value > 100000000000000) return (int)Errorcodes.toBigPrice;
                    
                    var filter = Builders<Konta>.Filter.Eq("_id", account);
                    var update = Builders<Konta>.Update.Set("balance", user[0].balance - value);
                    coll.UpdateOne(filter, update);
                    var transferfilter = Builders<Konta>.Filter.Eq("_id", transferAccount);
                    var transferupdate = Builders<Konta>.Update.Set("balance", transfer[0].balance + value);
                    coll.UpdateOne(transferfilter, transferupdate);
                    AddHistory(transferAccount, "przelew od " + account + " kwota ", value);
                    AddHistory(account, "przelew na konto " + transferAccount + " kwota", value);
                    return (int)Errorcodes.ok;
                }
                else if (user[0].balance < value)
                    return (int)Errorcodes.noEnoughMoney;
                else
                    return (int)Errorcodes.wrongPrice;
            }
            public static int Deposit(string account, double value)
            {
                List<Konta> users = coll.Find(b => b._id == account).ToList();
                if (users[0].balance + value > 100000000000000) return (int)Errorcodes.toBigPrice;
                if (value > 0)
                {
                                
                    var filter = Builders<Konta>.Filter.Eq("_id", account);
                    var update = Builders<Konta>.Update.Set("balance", users[0].balance + value);
                    coll.UpdateOne(filter, update);
                    AddHistory(account, "wpłata", value);
                    return (int)Errorcodes.ok;
                    
                }
                else
                {
                    return (int)Errorcodes.wrongPrice;
                }
            }
            public static double GetBalance(string account)
            {
                List<Konta> user = coll.Find(b => b._id == account).ToList();
                double balance = Math.Floor(user[0].balance * 100) / 100;
                return balance;
            }

            public static void GetHistory(string account)
            {
                List<Konta> user = coll.Find(b => b._id == account).ToList();
                if (user[0].history.Length == 0)
                {
                    Console.WriteLine("Historia jest pusta");
                }
                else
                {
                    try
                    {
                        foreach (string hist in user[0].history)
                        {
                            Console.WriteLine(hist);
                        }
                    }
                    catch (NullReferenceException)
                    {
                        Console.WriteLine("Historia jest pusta");
                    }
                }

            }

            private static void AddHistory(string account, string statement, double price)
            {
                List<Konta> user = coll.Find(b => b._id == account).ToList();

                string hist = DateTime.Now.ToString() + " " + statement + " " + price + " zł. stan konta po transakcji: "+ GetBalance(account) + " zł.";
                var filter = Builders<Konta>.Filter.Eq("_id", account);
                var update = Builders<Konta>.Update.AddToSet("history", hist);
                coll.UpdateOne(filter, update);
            }
            public static int AddPayer(string account, Payer payer)
            {

                List<Konta> user = coll.Find(b => b._id == payer.account).ToList();
                if (user.Count == 0)
                {
                    return (int)Errorcodes.wrongAccount;
                }
                var filter = Builders<Konta>.Filter.Eq("_id", account);
                var update = Builders<Konta>.Update.AddToSet("payers", payer);
                coll.UpdateOne(filter, update);
                return (int)Errorcodes.ok;
            }
            public static int RemovePayer(string account, int id)
            {
                List<Konta> user = coll.Find(b => b._id == account).ToList();
                var filter = Builders<Konta>.Filter.Eq("_id", account);
                try
                {
                    var update = Builders<Konta>.Update.Pull("payers", user[0].payers[id - 1]);
                    coll.UpdateOne(filter, update);
                    return (int)Errorcodes.ok;
                }
                catch (IndexOutOfRangeException) 
                {
                    return (int)Errorcodes.wrongAccount;
                }


            }
            public static void PayersList(string account)
            {
                List<Konta> user = coll.Find(b => b._id == account).ToList();
                try
                {
                    foreach (Payer payer in user[0].payers)
                    {
                        Console.WriteLine("[" + Convert.ToInt32(Array.IndexOf(user[0].payers, payer) + 1) + "] Nr konta: " + payer.account + " nazwa: " + payer.nick);
                    }
                }
                catch (NullReferenceException)
                {
                    Console.WriteLine("Brak zdefiniowanych płatników");
                }
            }
            public static string SelectPayer(string account, int id)
            {
                List<Konta> user = coll.Find(b => b._id == account).ToList();

                try
                {
                    return user[0].payers[id - 1].account;
                }catch(IndexOutOfRangeException)
                {
                    return " ";
                }


            }
            public static int NewAccount(string account, string password)
            {

                List<Konta> user = coll.Find(b => b._id == account).ToList();
                if (user.Count == 1)
                    return (int)Errorcodes.accountExist;
                string[] historia = { };
                Payer[] payers = { };
                Konta konto = new Konta();
                konto._id = account;
                konto.pin = password;
                konto.balance = 0;
                konto.history = historia;
                konto.payers = payers;

                coll.InsertOne(konto);
                return (int)Errorcodes.ok;

            }
            public static void UsersList()
            {
                List<Konta> users = coll.Find(b => b._id != "admin").ToList();
                foreach(Konta user in users)
                {
                    Console.WriteLine("[" + Convert.ToInt32(users.IndexOf(user) + 1) + "] numer konta: " + user._id + " stan konta: " + user.balance + " zł");
                }
            }

            public static int GetUserHistory(int number)
            {
                List<Konta> users = coll.Find(b => b._id != "admin").ToList();
                try
                {
                    GetHistory(users[number - 1]._id);
                    Console.WriteLine();
                    if (users[number - 1].tries >= 3) Console.WriteLine("[Konto zablokowane]");
                    else Console.WriteLine("[Konto aktywne]");
                    return (int)Errorcodes.ok;
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("Takiego numeru nie ma na liście");
                    return (int) Errorcodes.wrongAccount;
                }
            }
            public static void BlockUser(bool block, int number)
            {
                List<Konta> users = coll.Find(b => b._id != "admin").ToList();
                var filter = Builders<Konta>.Filter.Eq("_id", users[number - 1]._id);
                if (block == true)
                {
                    var update = Builders<Konta>.Update.Set("tries", 3);
                    coll.UpdateOne(filter, update);
                }
                else
                {
                    var update = Builders<Konta>.Update.Set("tries", 0);
                    coll.UpdateOne(filter, update);
                }

                
                    
                    

            }

        }




        static void Main(string[] args)
        {
            BankomatStatus bankomat = new BankomatStatus(5, 10, 20, 50);
            string konto, pin;
            char wybor = '\0';
            char wybor2 = '\0';
            char adminwybor = '\0';

            while (true)
            {
                Console.Clear();
                Console.WriteLine("[1] Zaloguj");
                Console.WriteLine("[2] Zarejestruj");
                wybor2 = Console.ReadKey().KeyChar;

                switch (wybor2)
                {


                    case '1':
                       Console.Clear();
                       Console.WriteLine("Podaj numer konta: ");
                       konto = Console.ReadLine();
                       Console.Clear();
                       Console.WriteLine("Podaj pin: ");
                       pin = Console.ReadLine();
                            switch (BankManager.Login(konto, pin))
                            {
                                case (int)BankManager.Logincodes.ok:
                                    Console.Clear();
                                    Console.WriteLine("Zalogowano");
                                    System.Threading.Thread.Sleep(1000);

                                //--------------------------------------------------- admin
                                if (konto == "admin")
                                {
                                    while (adminwybor != '0')
                                    {
                                        Console.Clear();
                                        Console.WriteLine("[1] Lista użytkowników");
                                        Console.WriteLine("[2] Stan bankomatu");
                                        Console.WriteLine("[0] Wyloguj");
                                        adminwybor = Console.ReadKey().KeyChar;
                                        switch (adminwybor)
                                        {
                                            case '1':
                                                while (true)
                                                {
                                                    Console.Clear();
                                                    Console.WriteLine("[0] Powrót");
                                                    Console.WriteLine();
                                                    BankManager.UsersList();
                                                    Console.WriteLine();
                                                    Console.WriteLine("Wybierz konto które chcesz sprawdzić");
                                                    int nrlist = -1;
                                                    try { nrlist = Convert.ToInt32(Console.ReadLine()); }
                                                    catch (FormatException) { }
                                                    Console.Clear();
                                                    bool flag = true;
                                                    while (flag)
                                                    {
                                                        if (nrlist == 0) break;
                                                        if (BankManager.GetUserHistory(nrlist) == (int)BankManager.Errorcodes.ok)
                                                        {
                                                            Console.WriteLine("[1] Powrót");
                                                            Console.WriteLine("[2] Zablokuj");
                                                            Console.WriteLine("[3] Odblokuj");
                                                            switch (Console.ReadKey().KeyChar)
                                                            {
                                                                case '1':
                                                                    flag = false;
                                                                    break;
                                                                case '2':
                                                                    BankManager.BlockUser(true, nrlist);
                                                                    Console.Clear();
                                                                    Console.WriteLine("Konto zostało zablokowane");
                                                                    System.Threading.Thread.Sleep(1000);
                                                                    Console.ReadLine();
                                                                    Console.Clear();
                                                                    break;
                                                                case '3':
                                                                    BankManager.BlockUser(false, nrlist);
                                                                    Console.Clear();
                                                                    Console.WriteLine("Konto zostało odblokowane");
                                                                    System.Threading.Thread.Sleep(1000);
                                                                    Console.ReadLine();
                                                                    Console.Clear();
                                                                    break;
                                                                default:
                                                                    Console.Clear();
                                                                    Console.WriteLine("Operacja nie rozpoznana");
                                                                    System.Threading.Thread.Sleep(1000);
                                                                    Console.Clear();
                                                                    break;
                                                            }
                                                        }
                                                    }
                                                    if (nrlist == 0) break;
                                                }
                                                break;
                                            case '2':
                                                while (adminwybor != '0')
                                                {
                                                    
                                                    Console.Clear();
                                                    bankomat.Status();

                                                    Console.WriteLine("[1] Uzupełnij bankomat");
                                                    Console.WriteLine("[2] Historia wpłat i wypłat");
                                                    Console.WriteLine("[0] Cofnij");
                                                    adminwybor = Console.ReadKey().KeyChar;

                                                    switch (adminwybor)
                                                    {
                                                        case '1':
                                                            Console.Clear();
                                                            Console.WriteLine("Ile banknotów {200zł}");
                                                            long twohundred = (long)GetPrice();
                                                            ulong twohundredd = twohundred == -1 ? 0 : (ulong)twohundred;
                                                            Console.WriteLine("Ile banknotów {100zł}");
                                                            long hundred = (long)GetPrice();
                                                            ulong hundredd = hundred == -1 ? 0 : (ulong)hundred;
                                                            Console.WriteLine("Ile banknotów {50zł}");
                                                            long fifty = (long)GetPrice();
                                                            ulong fiftyy = fifty == -1 ? 0 : (ulong)fifty;
                                                            Console.WriteLine("Ile banknotów {20zł}");
                                                            long twenty = (long)GetPrice();
                                                            ulong twentyy = twenty == -1 ? 0 : (ulong)twenty;
                                                            bankomat.AddMoney(twohundredd, hundredd, fiftyy, twentyy);
                                                            Console.ReadLine();
                                                            break;
                                                        case '2':
                                                            Console.Clear();
                                                            bankomat.BankomatLog();
                                                            Console.WriteLine("Naciśnij ENTER aby wrócić");
                                                            Console.ReadLine();
                                                            break;
                                                        case '0':
                                                            break;
                                                        default:
                                                            Console.Clear();
                                                            Console.WriteLine("Operacja nie rozpoznana");
                                                            System.Threading.Thread.Sleep(1000);
                                                            Console.Clear();
                                                            break;
                                                    }


                                                }
                                                adminwybor = '\0';
                                                break;
                                            case '0':
                                                break;
                                            default:
                                                Console.Clear();
                                                Console.WriteLine("Operacja nie rozpoznana");
                                                System.Threading.Thread.Sleep(1000);
                                                break;
                                        }

                                    }
                                }

                                //--------------------------------------------------- normalne konto
                                else
                                {
                                    while (wybor != '6')
                                    {
                                        Console.Clear();
                                        Console.WriteLine("Saldo konta: " + BankManager.GetBalance(konto) + " zł" + Environment.NewLine);
                                        Console.WriteLine("Dostępne operacje:");
                                        Console.WriteLine("[1] Wypłać środki");
                                        Console.WriteLine("[2] Wpłać środki");
                                        Console.WriteLine("[3] Przelew");
                                        Console.WriteLine("[4] Historia rachunku");
                                        Console.WriteLine("[5] Lista zdefiniowanych płatników");
                                        Console.WriteLine("[6] Wyloguj");
                                        wybor = Console.ReadKey().KeyChar;
                                        switch (wybor)
                                        {
                                            case '1':
                                                Withdraw();
                                                break;
                                            case '2':
                                                Deposit();
                                                break;
                                            case '3':
                                                Transfer();
                                                break;
                                            case '4':
                                                History();
                                                break;
                                            case '5':
                                                PayersList();
                                                break;
                                            case '6':
                                                break;
                                            default:
                                                Console.Clear();
                                                Console.WriteLine("Operacja nie rozpoznana");
                                                System.Threading.Thread.Sleep(1000);
                                                break;
                                        }

                                    }
                                    wybor = '\0';
                                }

                                    Console.Clear();
                                    Console.WriteLine("Wylogowano");
                                    System.Threading.Thread.Sleep(1000);

                                    break;

                                case (int)BankManager.Logincodes.wrongLogin:
                                    Console.WriteLine("Nie ma takiego konta w bazie");
                                    Console.ReadLine();
                                    break;

                                case (int)BankManager.Logincodes.wrongPin:
                                    Console.WriteLine("Błędny kod pin");
                                    Console.ReadLine();
                                    break;

                                case (int)BankManager.Logincodes.blocked:
                                    Console.WriteLine("Konto zablokowane");
                                    Console.ReadLine();
                                    break;

                }
                        break;
                    #region rejestracja
                    case '2':
                        Console.Clear();
                        Console.WriteLine("Proszę podać numer nowego konta:");
                        string numer;
                        try { numer = Convert.ToInt32(Console.ReadLine()).ToString(); }
                        catch (FormatException)
                        {
                            Console.Clear();
                            Console.WriteLine("Numer konta musi składać się wyłącznie z cyfr");
                            System.Threading.Thread.Sleep(1000);
                            Console.ReadKey();
                            break;
                        }
                        
                        Console.WriteLine("Podaj pin");
                        string pass = Console.ReadLine();
                        if (pass == String.Empty) 
                        {
                            Console.Clear();
                            Console.WriteLine("Pin nie może być pusty"); 
                            System.Threading.Thread.Sleep(1000);
                            Console.ReadKey();
                            break;
                        }
                        Console.Clear();
                        Console.WriteLine("Powtórz hasło");
                        string pass2 = Console.ReadLine();
                        if (pass != pass2) 
                        {
                            Console.Clear();
                            Console.WriteLine("Podane numery pin róznią się.");
                            System.Threading.Thread.Sleep(1000);
                            Console.ReadKey();
                            break;
                        }

                        if (BankManager.NewAccount(numer, pass) == (int)BankManager.Errorcodes.ok)
                        {
                            Console.Clear();
                            Console.WriteLine("Konto zostało założone, możesz się teraz zalogować");
                            System.Threading.Thread.Sleep(1000);
                            Console.ReadKey();
                            break;
                        }
                        else
                        {
                            Console.Clear();
                            Console.WriteLine("Podany numer konta instnieje już w bazie");
                            System.Threading.Thread.Sleep(1000);
                            Console.ReadKey();
                            break;
                        }
                    #endregion

                    default:
                        Console.WriteLine("Operacja nie rozpoznana");
                        System.Threading.Thread.Sleep(1000);
                        break;


                }
            }

            void Withdraw()
            {
                double price;
                while (true)
                {
                    Console.Clear();
                    bankomat.Status();
                    Console.WriteLine("Wprowadź kwotę którą chcesz wypłacić, dostępne środki to " + BankManager.GetBalance(konto) + "zł, wpisz 0 aby anulować");
                    price = GetPrice();
                    if (price == 0) break;
                    Console.Clear();
                    if (bankomat.Withdraw(price, konto))
                    {
                        Console.ReadLine();
                        break;
                    }
                    else Console.ReadLine();
                    
                }
            }
            void Transfer()
            {
                double price = -1;
                string transferaccount;
                while (price != 0)
                {
                    Console.Clear();
                    Console.WriteLine("Wprowadź kwotę którą chcesz przelać, dostępne środki to " + BankManager.GetBalance(konto) + "zł, wpisz 0 aby anulować");
                    price = GetPrice();
                    if (price == 0) break;
                    Console.WriteLine("Podaj numer konta na które chcesz przelać pieniądze");
                    Console.WriteLine("[L] lista zdefiniowanych płatników");
                    transferaccount = Console.ReadLine().ToLower();
                    if(transferaccount == "l")
                    {
                        Console.Clear();
                        Console.WriteLine("Wpisz numer z listy płatnika do którego chcesz wykonać przelew");
                        BankManager.PayersList(konto);
                        Console.WriteLine(Environment.NewLine + "[0] Cofnij");
                        transferaccount = BankManager.SelectPayer(konto, (int)GetPrice());
                        if (transferaccount == "0") break;
                    }
                    switch(BankManager.Transfer(konto, price, transferaccount))
                    {
                        case (int)BankManager.Errorcodes.ok:
                            Console.Clear();
                            Console.WriteLine("Przelano " + price + "zł na rachunek " + transferaccount + ", obecny stan konta to:" + BankManager.GetBalance(konto) + "zł");
                            Console.ReadKey();
                            price = 0;
                            break;
                        case (int)BankManager.Errorcodes.noEnoughMoney:
                            Console.Clear();
                            Console.WriteLine("Niewystarczająca ilość środków na koncie");
                            Console.ReadKey();
                            break;
                        case (int)BankManager.Errorcodes.wrongPrice:
                            if (price == 0) break;
                            Console.Clear();
                            Console.WriteLine("Wprowadzono błędną kwote");
                            Console.ReadKey();
                            break;
                        case (int)BankManager.Errorcodes.wrongAccount:
                            Console.Clear();
                            Console.WriteLine("Wprowadzono zły numer konta odbiorcy");
                            Console.ReadKey();
                            break;
                        case (int)BankManager.Errorcodes.itsYourAccount:
                            Console.Clear();
                            Console.WriteLine("Nie można przelać pieniędzy na własne konto");
                            Console.ReadKey();
                            break;
                        case (int)BankManager.Errorcodes.toBigPrice:
                            Console.Clear();
                            Console.WriteLine("Nie można przelać takiej ilości gotówki na te konto");
                            Console.ReadKey();
                            break;
                    }

                }
            }
            void Deposit()
            {
                double price = -1;
                while (price != 0)
                {
                    Console.Clear();
                    Console.WriteLine("Wprowadź kwotę którą chcesz wpłacić, wpisz 0 aby anulować");
                    price = GetPrice();
                    switch (BankManager.Deposit(konto, price))
                    {
                        case (int)BankManager.Errorcodes.ok:
                            Console.WriteLine("Wpłacono " + price + "zł, obecny stan konta to:" + BankManager.GetBalance(konto) + "zł");
                            Console.ReadKey();
                            price = 0;
                            break;
                        case (int)BankManager.Errorcodes.wrongPrice:
                            if (price == 0) break;
                            Console.Clear();
                            Console.WriteLine("Wprowadzono błędną kwote");
                            Console.ReadKey();
                            break;
                        case (int)BankManager.Errorcodes.toBigPrice:
                            Console.Clear();
                            Console.WriteLine("Nie można wpłacic tak dużej kwoty");
                            Console.ReadKey();
                            break;

                    }

                }

            }
            void History()
            {
                Console.Clear();
                Console.WriteLine("Historia rachunku "+ konto);
                BankManager.GetHistory(konto);
                Console.WriteLine();
                Console.WriteLine("naciśnij ENTER aby wyjść");
                Console.ReadLine();
            }
            void PayersList()
            {
                wybor = '\0';
                while (wybor != '3')
                {
                    Console.Clear();
                    Console.WriteLine("Zdefiniowani płatnicy:");
                    BankManager.PayersList(konto);
                    Console.WriteLine();
                    Console.WriteLine("(1) Dodaj płatnika");
                    Console.WriteLine("(2) Usuń płatnika");
                    Console.WriteLine("(3) Powrót");
                    wybor = Console.ReadKey().KeyChar;
                    switch (wybor)
                    {
                        case '1':
                            Console.Clear();
                            Payer payer = new Payer();
                            Console.WriteLine("Podaj nr konta, wpisz 0 aby cofnąć");
                            payer.account = Console.ReadLine();
                            if (payer.account == "0") break;
                            Console.Clear();
                            Console.WriteLine("Jak zapisać płatnika");
                            payer.nick = Console.ReadLine();
                            Console.Clear();
                            if (BankManager.AddPayer(konto, payer) == (int)BankManager.Errorcodes.wrongAccount)
                            {
                                Console.WriteLine("Podany numer konta nie istnieje");
                                Console.ReadKey();
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Dodano nowego płatnika.");
                                Console.ReadKey();
                                break;
                            }
                        case '2':
                            Console.Clear();
                            Console.WriteLine("Podaj numer z listy płatnika którego chcesz usunąć, wpisz 0 aby cofnąć");
                            BankManager.PayersList(konto);
                            int numer = (int)GetPrice();
                            if (numer == 0) break;
                            if (BankManager.RemovePayer(konto, numer) == (int)BankManager.Errorcodes.ok) 
                            {
                                Console.Clear();
                                Console.WriteLine("Usunięto płatnika.");
                                Console.ReadKey();
                                break;
                            } else 
                            {
                                Console.Clear();
                                Console.WriteLine("Wprowadzono zły numer.");
                                Console.ReadKey();
                                break;
                            }

                        case '3': 
                            break;
                        default :
                            Console.Clear();
                            Console.WriteLine("Operacja nie rozpoznana");
                            System.Threading.Thread.Sleep(1000);
                            break;

                    }




                }

            }


            static double GetPrice()
            {
                double price;
                try
                {
                    price = Math.Floor(Convert.ToDouble(Console.ReadLine()) * 100) / 100;
                }
                catch (FormatException)
                {
                    return - 1;
                }

                return price;
            }



        }
    }
}
