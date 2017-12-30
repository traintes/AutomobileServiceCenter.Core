using ASC.DataAccess;
using ASC.DataAccess.Interfaces;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Threading.Tasks;

namespace AzureStorageTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Task t = MainAsync();
            t.Wait();
        }

        static async Task MainAsync()
        {
            // Create a book
            using(UnitOfWork _unitOfWork = new UnitOfWork("UseDevelopmentStorage=true"))
            {
                IRepository<Book> bookRepository = _unitOfWork.Repository<Book>();
                await bookRepository.CreateTableAsync();

                Book book = new Book()
                {
                    Author = "Rami",
                    BookName = "ASP.NET Core With Azure",
                    Publisher = "Apress"
                };
                book.BookId = 1;
                book.RowKey = book.BookId.ToString();
                book.PartitionKey = book.Publisher;

                Book data = await bookRepository.AddAsync(book);
                Console.WriteLine(data);

                _unitOfWork.CommitTransaction();
            }

            // Update a book
            using(var _unitOfWork = new UnitOfWork("UseDevelopmentStorage=true"))
            {
                IRepository<Book> bookRepository = _unitOfWork.Repository<Book>();
                await bookRepository.CreateTableAsync();

                Book data = await bookRepository.FindAsync("Apress", "1");
                Console.WriteLine(data);

                data.Author = "Rami Vemula";
                Book updatedData = await bookRepository.UpdateAsync(data);
                Console.WriteLine(updatedData);

                _unitOfWork.CommitTransaction();
            }

            // Delete a book
            using(var _unitOfWork = new UnitOfWork("UseDevelopmentStorage=true"))
            {
                IRepository<Book> bookRepository = _unitOfWork.Repository<Book>();
                await bookRepository.CreateTableAsync();

                Book data = await bookRepository.FindAsync("Apress", "1");
                Console.WriteLine(data);

                await bookRepository.DeleteAsync(data);
                Console.WriteLine("Deleted");

                // Throw an exception to test rollback actions
                // throw new Exception();

                _unitOfWork.CommitTransaction();
            }

            Console.ReadLine();
        }
    }
}
