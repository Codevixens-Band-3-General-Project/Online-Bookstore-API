﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookstoreAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using BookstoreAPI.Extensions;
using BookstoreAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Data;

namespace BookstoreAPI.Controllers
{
    [Authorize(AuthenticationSchemes = "Bearer")] // Require JWT Bearer token authentication for accessing the controller
    [ApiController]
    [Route("[controller]")]
    public class BookController : ControllerBase
    {
        private readonly ILogger<BookController> _logger;
        private readonly ApplicationDbContext _db;

        public BookController(ILogger<BookController> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        // GET: /book/all
        [HttpGet("all")]
        public ActionResult<IEnumerable<Book>> Get()
        {
            try
            {
                // Retrieve all books from the database
                var books = _db.Books.ToList();
                return Ok(books);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve books.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve books. Please try again later.");
            }
        }

        // GET: /book/search
        [HttpGet("search")]
        public ActionResult<IEnumerable<Book>> Search(string searchTerm, string filter)
        {
            try
            {
                // Perform a search based on the provided search term and filter
                var books = _db.Books.AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm) && !string.IsNullOrEmpty(filter))
                {
                    switch (filter.ToLower())
                    {
                        case "title":
                            // Split the search term by spaces to get individual keywords
                            var titleKeywords = searchTerm.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            // Require at least two or three relevant keywords for the search to appear
                            if (titleKeywords.Length >= 2)
                            {
                                books = books.ToList().Where(b => titleKeywords.All(k => b.BookTitle.ToLower().Contains(k))).AsQueryable();
                            }
                            else
                            {
                                books = Enumerable.Empty<Book>().AsQueryable();
                            }
                            break;
                        case "author":
                            // Split the search term by spaces to get individual keywords
                            var authorKeywords = searchTerm.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            books = books.ToList().Where(b => authorKeywords.Any(k => b.BookAuthor.ToLower().Contains(k))).AsQueryable();
                            break;
                        case "genre":
                            // Split the search term by spaces to get individual keywords
                            var genreKeywords = searchTerm.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            books = books.ToList().Where(b => genreKeywords.Any(k => b.Genre.ToLower().Contains(k))).AsQueryable();
                            break;
                        default:
                            return BadRequest("Invalid filter parameter.");
                    }
                }
                var filteredBooks = books.ToList();

                return Ok(filteredBooks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search for books.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to search for books. Please try again later.");
            }

        }


        // GET: /book/get-by-id/{id}
        [HttpGet("get-by-id/{id:int}")]
        public ActionResult<Book> Get(int id)
        {
            try
            {
                // Retrieve a book by its ID
                var book = _db.Books.Find(id);

                if (book == null)
                {
                    return NotFound();
                }

                return book;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, $"Failed to retrieve book with ID : {id}.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to retrieve book with ID: {id}.");
            }
        }

        // GET: /book/get-by-genre/{genre}
        [HttpGet("get-by-genre/{genre}")]
        public ActionResult<IEnumerable<Book>> GetByGenre(string genre)
        {
            try
            {
                // Retrieve books by genre
                var books = _db.Books
                .Where(b => b.Genre.ToLower().Contains(genre.ToLower()))
                .ToList();

                if (books.Count == 0)
                {
                    return NotFound();
                }

                return Ok(books);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve books by genre.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve books by genre. Please try again later.");
            }


        }

        // GET: /book/get-by-author/{author}
        [HttpGet("get-by-author/{author}")]
        public ActionResult<IEnumerable<Book>> GetByAuthor(string author)
        {
            try
            {
                // Retrieve books by author
                var books = _db.Books
                    .Where(b => b.BookAuthor.ToLower().Contains(author.ToLower()))
                    .ToList();

                if (books.Count == 0)
                {
                    return NotFound();
                }

                return Ok(books);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve books by author(s).");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve books by author(s). Please try again later.");
            }
        }

        // POST: /book/create
        [HttpPost("create")]
        [Authorize(Roles = "Admin")] // Restrict access to only users with the "Admin" role
        public async Task<ActionResult<Book>> CreateBook([FromForm] Book book)
        {
            try
            {
                // Check if a book with the same title, author, and year of publication already exists
                var existingBook = await _db.Books.FirstOrDefaultAsync(b =>
                    b.BookTitle == book.BookTitle &&
                    b.BookAuthor == book.BookAuthor &&
                    b.YearOfPublication == book.YearOfPublication);

                if (existingBook != null)
                {
                    return Conflict("A book with the same title, author, and year of publication already exists.");
                }
                // Create a new book and save it to the database
                Book newBook = new Book
                {
                    BookTitle = book.BookTitle,
                    YearOfPublication = book.YearOfPublication,
                    Publisher = book.Publisher
                };

                // Split the authors by comma and remove any leading or trailing spaces
                if (!string.IsNullOrEmpty(book.BookAuthor))
                {
                    string[] authors = book.BookAuthor.Split(',').Select(a => a.Trim()).ToArray();
                    newBook.BookAuthor = string.Join(", ", authors);
                }

                // Split the genres by comma and remove any leading or trailing spaces
                if (!string.IsNullOrEmpty(book.Genre))
                {
                    string[] genres = book.Genre.Split(',').Select(g => g.Trim()).ToArray();
                    newBook.Genre = string.Join(", ", genres);
                }

                await _db.Books.AddAsync(newBook);
                await _db.SaveChangesAsync();

                return CreatedAtAction(nameof(Get), new { id = newBook.Id }, new { message = $"Book '{newBook.BookTitle}' created successfully." });

            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create a book.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create a book. Please try again later.");
            }
        }

        // PUT: /book/update/{id}
        [HttpPut("update/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromForm] Book updatedBook)
        {
            try
            {
                // Update an existing book based on its ID
                var existingBook = await _db.Books.FindAsync(id);

                if (existingBook == null)
                {
                    return NotFound();
                }

                var updatedParameters = new List<string>();

                if (!string.IsNullOrEmpty(updatedBook.BookTitle) && existingBook.BookTitle != updatedBook.BookTitle)
                {
                    existingBook.BookTitle = updatedBook.BookTitle;
                    updatedParameters.Add("Book Title");
                }

                if (!string.IsNullOrEmpty(updatedBook.BookAuthor) && existingBook.BookAuthor != updatedBook.BookAuthor)
                {
                    existingBook.BookAuthor = updatedBook.BookAuthor;
                    updatedParameters.Add("Book Author");
                }

                if (!string.IsNullOrEmpty(updatedBook.Genre) && existingBook.Genre != updatedBook.Genre)
                {
                    existingBook.Genre = updatedBook.Genre;
                    updatedParameters.Add("Genre");
                }

                if (updatedBook.YearOfPublication.HasValue && existingBook.YearOfPublication != updatedBook.YearOfPublication)
                {
                    existingBook.YearOfPublication = updatedBook.YearOfPublication;
                    updatedParameters.Add("Year of Publication");
                }

                if (!string.IsNullOrEmpty(updatedBook.Publisher) && existingBook.Publisher != updatedBook.Publisher)
                {
                    existingBook.Publisher = updatedBook.Publisher;
                    updatedParameters.Add("Publisher");
                }

                await _db.SaveChangesAsync();

                var message = $"Book with ID {id} updated. Updated parameters: {string.Join(", ", updatedParameters)}";
                return Ok(new { message });
            }


            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update the book with ID: {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to update the book with ID: {id}. Please try again later.");
            }
        }

        // DELETE: /book/delete/{id}
        [HttpDelete("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Delete a book based on its ID
                var book = await _db.Books.FindAsync(id);

                if (book == null)
                {
                    return NotFound();
                }

                _db.Books.Remove(book);
                await _db.SaveChangesAsync();

                return Ok($"Book-ID {id} Title-{book.BookTitle} deleted from inventory");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete the book with ID: {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to delete the book with ID: {id}. Please try again later.");
            }
        }


        // POST: /book/add-to-cart/{id}
        [HttpPost("add-to-cart/{id:int}")]
        public async Task<ActionResult> AddToCart(int id)
        {
            try
            {
                // Add a book to the shopping cart
                var book = await _db.Books.FindAsync(id);

                if (book == null)
                {
                    return NotFound();
                }

                List<Book> cart = HttpContext.Session.GetObjectFromJson<List<Book>>("Cart") ?? new List<Book>();

                cart.Add(book);

                HttpContext.Session.SetObjectAsJson("Cart", cart);

                return Ok($"Book-ID {id} Title-{book.BookTitle} has been added to cart");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to add the book with ID: {id} to the cart.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to add the book with ID: {id} to the cart. Please try again later.");
            }
        }

        // POST: /book/delete-from-cart/{id}
        [HttpPost("delete-from-cart/{id:int}")]
        public ActionResult DeleteFromCart(int id)
        {
           try
           {
                // Remove a book from the shopping cart
                List<Book> cart = HttpContext.Session.GetObjectFromJson<List<Book>>("Cart");

                if (cart == null)
                {
                    return NotFound();
                }

                var book = cart.FirstOrDefault(b => b.Id == id);

                if (book != null)
                {
                    cart.Remove(book);
                    HttpContext.Session.SetObjectAsJson("Cart", cart);
                }

                return Ok($"Book-ID {id} Title-{book.BookTitle} has been removed from cart");
           }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete the book with ID: {id} from the cart.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to delete the book with ID: {id} from the cart. Please try again later.");
            }

        }

        // POST: /book/view-cart/{id}
        [HttpGet("view-cart")]
        public ActionResult<IEnumerable<Book>> ViewCart()
        {
            try
            {
                // View the contents of the shopping cart
                List<Book> cart = HttpContext.Session.GetObjectFromJson<List<Book>>("Cart");
                if (cart == null)
                {
                    return StatusCode(StatusCodes.Status404NotFound, "Cart is empty, please add a book to view cart.");
                }
                return Ok(cart);
            }
            
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to view the cart.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to view the cart. Please try again later.");
            }
        }
    }
}