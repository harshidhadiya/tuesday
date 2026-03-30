using AutoMapper;
using CloudinaryService;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Data.Dto.Response;
using PRODUCT.Model;
using PRODUCT.Repository;
using PRODUCT.Services;

namespace Product.Tests.Services
{
    public class ProductServiceTests
    {
        private readonly Mock<Irepository> _repo;
        private readonly Mock<IMapper> _mapper;
        private readonly Mock<ClodinaryService> _cloudinary;
        private readonly Mock<IPublishEndpoint> _publish;
        private readonly Mock<ILogger<ProductService>> _logger;
        private readonly ProductService _sut; // system under test

        public ProductServiceTests()
        {
            _repo      = new Mock<Irepository>();
            _mapper    = new Mock<IMapper>();
            var configMock = new Mock<IConfiguration>();
            _cloudinary = new Mock<ClodinaryService>(configMock.Object);
            _publish   = new Mock<IPublishEndpoint>();
            _logger    = new Mock<ILogger<ProductService>>();

            _sut = new ProductService(
                _repo.Object,
                _mapper.Object,
                _publish.Object,
                _cloudinary.Object,
                _logger.Object);
        }

        [Fact]
        public async Task CreateProduct_Should_Return_400_When_Product_Name_Already_Exists()
        {
            // Arrange
            var request = new ProductCreate { name = "ExistingProduct" };
            _repo.Setup(r => r.exist("ExistingProduct")).ReturnsAsync(true);

            // Act
            var result = await _sut.createProduct(request);

            // Assert
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(400);
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("already exist");

            _repo.Verify(r => r.Add(It.IsAny<ProductTable>()), Times.Never);
        }

        [Fact]
        public async Task CreateProduct_Should_Return_200_When_Product_Created_Without_Images()
        {
            // Arrange
            var request = new ProductCreate { name = "NewProduct", images = null };
            var entity  = new ProductTable { Id = 1, product_name = "NewProduct", user_id = 42 };
            var dto     = new ProductDto  { id = 1, Name = "NewProduct" };

            _repo.Setup(r => r.exist("NewProduct")).ReturnsAsync(false);
            _mapper.Setup(m => m.Map<ProductTable>(request)).Returns(entity);
            _repo.Setup(r => r.Add(entity)).ReturnsAsync(entity);
            _mapper.Setup(m => m.Map<ProductDto>(entity)).Returns(dto);

            // Act
            var result = await _sut.createProduct(request);

            // Assert
            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.id.Should().Be(1);

            _publish.Verify(p => p.Publish(It.IsAny<ProductCreatedForVerification>(), default), Times.Once);
            _repo.Verify(r => r.Add(entity), Times.Once);
        }

        [Fact]
        public async Task CreateProduct_Should_Upload_Images_And_Return_200()
        {
            // Arrange
            var fakeFile = CreateFakeFile();
            var request  = new ProductCreate { name = "ImageProduct", images = new List<IFormFile> { fakeFile } };
            var entity   = new ProductTable { Id = 2, product_name = "ImageProduct", user_id = 10 };
            var dto      = new ProductDto { id = 2 };

            _repo.Setup(r => r.exist("ImageProduct")).ReturnsAsync(false);
            _mapper.Setup(m => m.Map<ProductTable>(request)).Returns(entity);
            _cloudinary.Setup(c => c.singleUpload(fakeFile))
                       .ReturnsAsync(("http://img.url", "publicId123"));
            _repo.Setup(r => r.Add(It.IsAny<ProductTable>())).ReturnsAsync(entity);
            _mapper.Setup(m => m.Map<ProductDto>(entity)).Returns(dto);

            // Act
            var result = await _sut.createProduct(request);

            // Assert
            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            _cloudinary.Verify(c => c.singleUpload(fakeFile), Times.Once);

            _publish.Verify(p => p.Publish(It.IsAny<ProductCreatedForVerification>(), default), Times.Once);
        }


        [Fact]
        public async Task DeleteProduct_Should_Return_404_When_Product_Not_Found()
        {
            // Arrange
            _repo.Setup(r => r.getByIdProduct(99)).ReturnsAsync((ProductTable?)null);

            // Act
            var result = await _sut.deleteProduct(99, 1);

            // Assert
            result.StatusCode.Should().Be(404);
            result.Success.Should().BeFalse();
            _repo.Verify(r => r.deleteProduct(It.IsAny<ProductTable>()), Times.Never);
        }

        [Fact]
        public async Task DeleteProduct_Should_Return_403_When_User_Is_Not_Owner()
        {
            // Arrange
            var product = new ProductTable { Id = 1, user_id = 5 };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act — caller is user 99, owner is 5
            var result = await _sut.deleteProduct(1, 99);

            // Assert
            result.StatusCode.Should().Be(403);
            result.Success.Should().BeFalse();
            _repo.Verify(r => r.deleteProduct(It.IsAny<ProductTable>()), Times.Never);
        }

        [Fact]
        public async Task DeleteProduct_Should_Return_500_When_Repository_Returns_Null()
        {
            // Arrange
            var product = new ProductTable { Id = 1, user_id = 7 };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);
            _repo.Setup(r => r.deleteProduct(product)).ReturnsAsync((ProductTable?)null);

            // Act
            var result = await _sut.deleteProduct(1, 7);

            // Assert
            result.StatusCode.Should().Be(500);
            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteProduct_Should_Return_200_And_Publish_Event_When_Owner_Deletes()
        {
            // Arrange
            var product = new ProductTable { Id = 3, user_id = 7 };
            var dto     = new ProductDto { id = 3 };

            _repo.Setup(r => r.getByIdProduct(3)).ReturnsAsync(product);
            _repo.Setup(r => r.deleteProduct(product)).ReturnsAsync(product);
            _mapper.Setup(m => m.Map<ProductDto>(product)).Returns(dto);

            // Act
            var result = await _sut.deleteProduct(3, 7);

            // Assert
            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            result.Data!.id.Should().Be(3);

            _publish.Verify(p => p.Publish(It.IsAny<ProductDeleted>(), default), Times.Once);
        }
        

        [Fact]
        public async Task UpdateProduct_Should_Return_404_When_Product_Not_Found()
        {
            // Arrange
            var update = new ProductUpdate { id = 5 };
            _repo.Setup(r => r.getByIdProduct(5)).ReturnsAsync((ProductTable?)null);

            // Act
            var result = await _sut.updateProduct(update, 1);

            // Assert
            result.StatusCode.Should().Be(404);
            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateProduct_Should_Return_403_When_User_Is_Not_Owner()
        {
            // Arrange
            var update  = new ProductUpdate { id = 1 };
            var product = new ProductTable { Id = 1, user_id = 10 };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act
            var result = await _sut.updateProduct(update, 99);

            // Assert
            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task UpdateProduct_Should_Return_400_When_Setting_AuctionDates_On_Unverified_Product()
        {
            // Arrange
            var future  = DateTime.Now.AddDays(2);
            var update  = new ProductUpdate
            {
                id               = 1,
                AuctionStartTime = future,
                AuctionEndTime   = future.AddHours(2)
            };
            var product = new ProductTable { Id = 1, user_id = 1, isVerified = false };

            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act
            var result = await _sut.updateProduct(update, 1);

            // Assert
            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("not verified");
        }

        [Fact]
        public async Task UpdateProduct_Should_Return_200_And_Publish_When_Name_Changed()
        {
            // Arrange
            var update  = new ProductUpdate { id = 1, name = "Updated Name" };
            var product = new ProductTable { Id = 1, user_id = 1, isVerified = true };
            var dto     = new ProductDto { id = 1, Name = "Updated Name" };

            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);
            _repo.Setup(r => r.Update(product)).ReturnsAsync(product);
            _mapper.Setup(m => m.Map<ProductDto>(product)).Returns(dto);

            // Act
            var result = await _sut.updateProduct(update, 1);

            // Assert
            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();

            _publish.Verify(p => p.Publish(It.IsAny<ProductUpdateForVerification>(), default), Times.Once);
        }

        [Fact]
        public async Task UpdateProduct_Should_Not_Publish_When_Only_Date_Changed()
        {
            // Arrange
            var update  = new ProductUpdate { id = 1, date = DateTime.Now.AddDays(1) };
            var product = new ProductTable { Id = 1, user_id = 1, isVerified = true };
            var dto     = new ProductDto { id = 1 };

            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);
            _repo.Setup(r => r.Update(product)).ReturnsAsync(product);
            _mapper.Setup(m => m.Map<ProductDto>(product)).Returns(dto);

            // Act
            var result = await _sut.updateProduct(update, 1);

            // Assert
            result.StatusCode.Should().Be(200);
            // no name/description changed → no publish
            _publish.Verify(p => p.Publish(It.IsAny<ProductUpdateForVerification>(), default), Times.Never);
        }



        [Fact]
        public async Task GetAllProducts_Should_Return_404_When_Repository_Returns_Null()
        {
            // Arrange
            var query = new ProductAll();
            _repo.Setup(r => r.AllProducts(query)).ReturnsAsync((IEnumerable<ProductTable>?)null);

            // Act
            var result = await _sut.getAllProducts(query);

            // Assert
            result.StatusCode.Should().Be(404);
            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task GetAllProducts_Should_Return_200_With_Mapped_List()
        {
            // Arrange
            var query    = new ProductAll();
            var entities = new List<ProductTable>
            {
                new ProductTable { Id = 1 },
                new ProductTable { Id = 2 }
            };
            var dtos = new List<ProductDto>
            {
                new ProductDto { id = 1 },
                new ProductDto { id = 2 }
            };

            _repo.Setup(r => r.AllProducts(query)).ReturnsAsync(entities);
            _mapper.Setup(m => m.Map<List<ProductDto>>(entities)).Returns(dtos);

            // Act
            var result = await _sut.getAllProducts(query);

            // Assert
            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            result.Data.Should().HaveCount(2);
        }



        [Fact]
        public async Task AddImage_Should_Return_404_When_Product_Not_Found()
        {
            // Arrange
            var query = new AddImage { id = 99 };
            _repo.Setup(r => r.getByIdProduct(99)).ReturnsAsync((ProductTable?)null);

            // Act
            var result = await _sut.addImage(query, 1);

            // Assert
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task AddImage_Should_Return_403_When_User_Is_Not_Owner()
        {
            // Arrange
            var product = new ProductTable { Id = 1, user_id = 5 };
            var query   = new AddImage { id = 1 };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act
            var result = await _sut.addImage(query, 99);

            // Assert
            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task AddImage_Should_Return_400_When_No_Images_Provided()
        {
            // Arrange
            var product = new ProductTable { Id = 1, user_id = 1 };
            var query   = new AddImage { id = 1, images = null };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act
            var result = await _sut.addImage(query, 1);

            // Assert
            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("No Images Found");
        }

        [Fact]
        public async Task AddImage_Should_Return_200_After_Successful_Upload()
        {
            // Arrange
            var fakeFile = CreateFakeFile();
            var product  = new ProductTable { Id = 1, user_id = 1, images = new List<ImageTable>() };
            var query    = new AddImage { id = 1, images = new List<IFormFile> { fakeFile } };
            var dto      = new ProductDto { id = 1 };

            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);
            _cloudinary.Setup(c => c.singleUpload(fakeFile))
                       .ReturnsAsync(("http://img.url", "pubId"));
            _repo.Setup(r => r.Update(It.IsAny<ProductTable>())).ReturnsAsync(product);
            _mapper.Setup(m => m.Map<ProductDto>(product)).Returns(dto);

            // Act
            var result = await _sut.addImage(query, 1);

            // Assert
            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            _cloudinary.Verify(c => c.singleUpload(fakeFile), Times.Once);
        }



        [Fact]
        public async Task DeleteProductImage_Should_Throw_KeyNotFoundException_When_Product_Not_Found()
        {
            // Arrange
            _repo.Setup(r => r.getByIdProduct(99)).ReturnsAsync((ProductTable?)null);

            // Act + Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _sut.deleteProductImage(99, 1, 1));
        }

        [Fact]
        public async Task DeleteProductImage_Should_Throw_UnauthorizedAccessException_When_User_Is_Not_Owner()
        {
            // Arrange
            var product = new ProductTable { Id = 1, user_id = 5, images = new List<ImageTable>() };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act + Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _sut.deleteProductImage(1, 1, 99));
        }

        [Fact]
        public async Task DeleteProductImage_Should_Throw_InvalidOperationException_When_Product_Has_No_Images()
        {
            // Arrange
            var product = new ProductTable { Id = 1, user_id = 1, images = new List<ImageTable>() };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.deleteProductImage(1, 1, 1));
        }

        [Fact]
        public async Task DeleteProductImage_Should_Throw_KeyNotFoundException_When_Image_Id_Not_Found()
        {
            // Arrange
            var product = new ProductTable
            {
                Id     = 1,
                user_id = 1,
                images = new List<ImageTable> { new ImageTable { Id = 10, public_Id = "pub10" } }
            };
            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);

            // Act + Assert — requesting image 999 which doesn't exist
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _sut.deleteProductImage(1, 999, 1));
        }

        [Fact]
        public async Task DeleteProductImage_Should_Return_200_And_Publish_When_Image_Deleted()
        {
            // Arrange
            var image   = new ImageTable { Id = 10, public_Id = "pub10" };
            var product = new ProductTable
            {
                Id     = 1,
                user_id = 1,
                images = new List<ImageTable> { image }
            };
            var dto = new ProductDto { id = 1 };

            _repo.Setup(r => r.getByIdProduct(1)).ReturnsAsync(product);
            _repo.Setup(r => r.Update(It.IsAny<ProductTable>())).ReturnsAsync(product);
            _mapper.Setup(m => m.Map<ProductDto>(product)).Returns(dto);

            // Act
            var result = await _sut.deleteProductImage(1, 10, 1);

            // Assert
            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            _publish.Verify(p => p.Publish(It.IsAny<productDeleteImage>(), default), Times.Once);
        }



        private static IFormFile CreateFakeFile()
        {
            var content  = "fake-image-content";
            var stream   = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
            fileMock.Setup(f => f.FileName).Returns("test.jpg");
            fileMock.Setup(f => f.Length).Returns(stream.Length);
            fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
            return fileMock.Object;
        }
    }
}
