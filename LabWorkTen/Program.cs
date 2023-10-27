using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YourNamespace.StockApp
{
    /// <summary>
    /// The migration has been added to change data in proccess 
    /// </summary>
    public class TickerContext : DbContext
    {
        public DbSet<Ticker> Tickers { get; set; }
        public DbSet<Price> Prices { get; set; }
        public DbSet<TodaysCondition> TodaysConditions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) // connection
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=TestDB;Trusted_Connection=True;MultipleActiveResultSets=true;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) //creates connection between tables by key
        {
            modelBuilder.Entity<Ticker>().HasKey(t => t.Id);
            modelBuilder.Entity<Price>().HasKey(p => p.Id);
            modelBuilder.Entity<TodaysCondition>().HasKey(tc => tc.Id);
        }
    }

    public class Ticker
    {
        public int Id { get; set; }
        public string TickerSymbol { get; set; }
    }

    public class Price
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public decimal PriceValue { get; set; }
        public DateTime Date { get; set; }
    }

    public class TodaysCondition
    {
        public int Id { get; set; }
        public int TickerId { get; set; }
        public string State { get; set; }
    }

    class Program
    {
        static async Task Main()
        {
            var str = ReadFromFile(); //read
            foreach (var st in str) //call for filling
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                await FillAsync(st);
            }

            Console.WriteLine("Введите тикер акции для проверки:");
            var ticker = Console.ReadLine(); // find the state

            using (var context = new TickerContext()) //LINQ to find
            {
                var todaysCondition = context.TodaysConditions //sum TC and Tickers
     .Join(context.Tickers,
           tc => tc.TickerId,
           t => t.Id,
           (tc, t) => new { TodaysCondition = tc, Ticker = t }) //do that by Id
     .Where(result => result.Ticker.TickerSymbol == ticker) //try to find ticker
     .OrderByDescending(result => result.TodaysCondition.Id) //sort
     .Select(result => result.TodaysCondition) //select TC
     .FirstOrDefault(); //show


                if (todaysCondition != null)
                {
                    string state = todaysCondition.State;
                    Console.WriteLine($"Ticker {ticker}: {state}");
                }
                else
                {
                    Console.WriteLine($"For this ticker {ticker} there is no data");
                }
            }
           

            static List<string> ReadFromFile()
            {
                List<string> tickers = File.ReadAllLines(@"C:\Users\gkras\OneDrive\Рабочий стол\ticker.txt").ToList();
                Console.WriteLine("File has been read= " + tickers[0]);
                return tickers;
            }
            static async Task FillAsync(string ticker) //using two apis
            {
                string apiUrlFirst = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1=1697217000&period2=1697217280&interval=1d&events=history&includeAdjustedClose=true"; ;
                string apiUrlSecond = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1=1697217290&period2=1697218290&interval=1d&events=history&includeAdjustedClose=true"; ;
                decimal firstPrice = 0;
                decimal secondPrice = 0;

                using (HttpClient client = new HttpClient()) //first price
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
                    }
                }

                using (HttpClient client = new HttpClient()) //second price
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

                using (var context = new TickerContext()) //adding data
                {
                    var newTicker = new Ticker { TickerSymbol = ticker };
                    context.Tickers.Add(newTicker);
                    context.SaveChanges();

                    var newPrice = new Price
                    {
                        TickerId = newTicker.Id,
                        PriceValue = secondPrice,
                        Date = DateTime.Now
                    };
                    context.Prices.Add(newPrice);

                    string statement = firstPrice < secondPrice ? "Has risen" : "Has fallen";
                    var newTodaysCondition = new TodaysCondition
                    {
                        TickerId = newTicker.Id,
                        State = statement
                    };
                    context.TodaysConditions.Add(newTodaysCondition);
                    context.SaveChanges();
                }
            }

        }
    }
}

