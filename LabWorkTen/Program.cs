using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DB
{
    public interface IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        int Id { get; set; }
    }

    public class TodaysCondition : IEntity
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public string State { get; set; }
    }

    public class Tickers : IEntity
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
    }

    public class Prices : IEntity
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
    }
    public class FinanceContext : DbContext
    {
        public DbSet<TodaysCondition> TodaysConditionsDB { get; set; }
        public DbSet<Tickers> TickersDB { get; set; }
        public DbSet<Prices> PricesDB { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=FinanceDB;Trusted_Connection=True;MultipleActiveResultSets=true;");


        }
    }

    public class FillFinanceDB
    {
        public List<string> ReadFromFile()
        {
            List<string> tickers = File.ReadAllLines(@"C:\Users\gkras\OneDrive\Рабочий стол\ticker.txt").ToList();
            Console.WriteLine("File has been read= " + tickers[1]);
            return tickers;
        }
        public async Task FillAsync(string ticker)
        {
            string apiUrlFirst = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1=1697217000&period2=1697217273&interval=1d&events=history&includeAdjustedClose=true";
            string apiUrlSecond = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1=1697217000&period2=1697217273&interval=1d&events=history&includeAdjustedClose=true";
            Console.WriteLine("api1 " + apiUrlFirst);
            Console.WriteLine("api2 " + apiUrlSecond);
            decimal firstPrice = 0;
            decimal secondPrice = 0;
            
            using (HttpClient client = new HttpClient())
            {
                string responseBody = await client.GetStringAsync(apiUrlFirst);
                string[] lines = responseBody.Split('\n');
                lines = lines.Skip(1).ToArray();

                foreach (string line in lines)
                {
                    string[] values = line.Split(',');
                    decimal high = Convert.ToDecimal(values[2], CultureInfo.InvariantCulture);
                    decimal low = Convert.ToDecimal(values[3], CultureInfo.InvariantCulture);
                    firstPrice = (high + low) / 2;
                    Console.WriteLine("price1 " + firstPrice);
                }
            }

            using (HttpClient client = new HttpClient())
            {
                string responseBody = await client.GetStringAsync(apiUrlSecond);
                string[] lines = responseBody.Split('\n');
                lines = lines.Skip(1).ToArray();

                foreach (string line in lines)
                {
                    string[] values = line.Split(',');
                    decimal high = Convert.ToDecimal(values[2], CultureInfo.InvariantCulture);
                    decimal low = Convert.ToDecimal(values[3], CultureInfo.InvariantCulture);
                    secondPrice = (high + low) / 2;
                }
            }

            using (var context = new FinanceContext())
            {
                var newTicker = new Tickers { Ticker = ticker };
                context.TickersDB.Add(newTicker);
                context.SaveChanges();

                var newPrice = new Prices
                {
                    TickerId = newTicker.Id,
                    Price = secondPrice,
                    Date = DateTime.Now
                };
                context.PricesDB.Add(newPrice);

                string statement = firstPrice < secondPrice ? "Has risen" : "Has fallen";
                var newTodaysCondition = new TodaysCondition
                {
                    TickerId = newTicker.Id,
                    State = statement
                };
                context.TodaysConditionsDB.Add(newTodaysCondition);
                context.SaveChanges();
            }
        }
    }

    class Program
    {
        static async Task Main()
        {
            FillFinanceDB fillFinanceDB = new FillFinanceDB();
            List<string> str = fillFinanceDB.ReadFromFile();
            foreach (var st in str)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                await fillFinanceDB.FillAsync(st);
            }

            Console.WriteLine("Введите тикер акции для проверки:");
            string ticker = Console.ReadLine();

            using (var context = new FinanceContext())
            {
                var todaysCondition = context.TodaysConditionsDB
                    .Include(tc => tc.TickerId)  // Если у вас есть свойство навигации для Ticker, используйте его.
                    .Where(tc => tc.TickerId == context.TickersDB
                                        .Where(t => t.Ticker == ticker)
                                        .Select(t => t.Id)
                                        .FirstOrDefault())
                    .OrderByDescending(tc => tc.Id)
                    .FirstOrDefault();

                if (todaysCondition != null)
                {
                    string state = todaysCondition.State;
                    Console.WriteLine($"Состояние акции для тикера {ticker}: {state}");
                }
                else
                {
                    Console.WriteLine($"Для тикера {ticker} нет данных о состоянии акции.");
                }
            }
            Console.ReadLine(); // Ожидание ввода пользователя перед завершением программы
        }
    }
}
