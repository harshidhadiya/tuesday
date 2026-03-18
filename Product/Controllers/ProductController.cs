using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRODUCT.Data.Dto;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Services;

using PRODUCT.Data.Dto.Response;

namespace PRODUCT.Controllers
{
    [ApiController]
    [Route("api/Product")]
    public class ProductController : ControllerBase
    {
        IproductService service;
        ILogger<ProductController> logger;
        public ProductController(IproductService service, ILogger<ProductController> logger)
        {
            this.service = service;
            this.logger = logger;
        }
        [NonAction]
        public int? getId(HttpContext context)
        {
            if (int.TryParse(HttpContext.Items["id"]?.ToString(), out int userId))
                return userId;
            return null;

        }
        [HttpPost("")]
        [Authorize(Roles = "SELLER,USER")]
        public async Task<IActionResult> createProduct(ProductCreate product)
        {


            int? id = getId(HttpContext);
            if (id == null)
                return BadRequest(ApiResponse<Object>.ErrorResponse("Your Id is not valid in the token", 400));
            product.user_id = id;
            var data = await service.createProduct(product);

            if (!data.Success)
            {
                switch (data.StatusCode)
                {
                    case 400: return BadRequest(ApiResponse<object>.ErrorResponse(data.Message, data.StatusCode));

                }
            }

            return Ok(ApiResponse<ProductDto>.SuccessResponse(data.Data!, data.Message));
        }


        [HttpDelete("{productId:int}")]
        [Authorize(Roles = "SELLER,USER")]
        public async Task<IActionResult> deleteproduct(int productId)
        {
            int? id = getId(HttpContext);
            if (id == null)
                return BadRequest(ApiResponse<Object>.ErrorResponse("Your Id is not valid in the token", 400));


            var data = await service.deleteProduct(productId, (int)id);
            if (!data.Success)
            {
                switch (data.StatusCode)
                {

                    case 404: return NotFound(ApiResponse<object>.ErrorResponse(data.Message, 404));
                    case 403: return Forbid(data.Message);
                    default: return BadRequest();
                }
            }

            return Ok(ApiResponse<ProductDto>.SuccessResponse(data.Data!, data.Message));
        }
        // all the things you can update here like schedule the auction or another thing you have to update all the things you can do this one api points okay
        [HttpPatch("{productId:int}")]
        [Authorize(Roles = "SELLER,USER")]
        public async Task<IActionResult> updateproduct(int productId, [FromForm] ProductUpdate product)
        {
            int? id = getId(HttpContext);
            if (id == null)
                return BadRequest(ApiResponse<Object>.ErrorResponse("Your Id is not valid in the token", 400));

            product.id = productId;
            var updatedProduct = await service.updateProduct(product, (int)id);
            if (!updatedProduct.Success)
            {
                switch (updatedProduct.StatusCode)
                {

                    case 404: return NotFound(ApiResponse<object>.ErrorResponse(updatedProduct.Message, 404));
                    case 403: return Forbid(updatedProduct.Message);
                    default: return BadRequest();
                }
            }
            return Ok(ApiResponse<ProductDto>.SuccessResponse(updatedProduct.Data!, updatedProduct.Message, 200));
        }
        [HttpPost("all")]
        [Authorize(Roles = "SELLER,USER,ADMIN")]
        public async Task<IActionResult> getallProducts([FromBody] ProductAll query)
        {
            int? id = getId(HttpContext);
            if (id == null)
                return BadRequest(ApiResponse<Object>.ErrorResponse("Your Id is not valid in the token", 400));

            query.id = id;
            var products = await service.getAllProducts(query);
            if (!products.Success)
                return NotFound(ApiResponse<object>.ErrorResponse(products.Message, products.StatusCode));
            if (products.Data.Count == 0)
            {
                return NotFound(ApiResponse<object>.SuccessResponse(null, "0 product found", 404));
            }
            return Ok(ApiResponse<List<ProductDto>>.SuccessResponse(products.Data, products.Message));

        }
        [HttpPost("images")]
        [Authorize(Roles = "SELLER,USER")]
        public async Task<IActionResult> addImages([FromForm] AddImage query)
        {
            int? id = getId(HttpContext);
            if (id == null)
                return BadRequest(ApiResponse<Object>.ErrorResponse("Your Id is not valid in the token", 400));

            var data = await service.addImage(query, (int)id);
            if (!data.Success)
            {
                switch (data.StatusCode)
                {
                    case 404: return NotFound(ApiResponse<object>.ErrorResponse(data.Message, 404));
                    case 403: return Forbid(data.Message);
                    default: return BadRequest(ApiResponse<object>.ErrorResponse(data.Message, data.StatusCode));
                }
            }

            return Ok(ApiResponse<ProductDto>.SuccessResponse(data.Data!, data.Message, 200));
        }
        [HttpDelete("{productId:int}/images/{imageId:int}")]
        [Authorize(Roles = "SELLER,USER")]
        public async Task<IActionResult> deleteProductImage(int productId, int imageId)
        {
            int? id = getId(HttpContext);
            if (id == null)
                return BadRequest(ApiResponse<Object>.ErrorResponse("Your Id is not valid in the token", 400));     
                var data = await service.deleteProductImage(productId, imageId, (int)id);
            if (!data.Success)            {
                switch (data.StatusCode)
                {
                    case 404: return NotFound(ApiResponse<object>.ErrorResponse(data.Message, 404));
                    case 403: return Forbid(data.Message);
                    default: return BadRequest(ApiResponse<object>.ErrorResponse(data.Message, data.StatusCode));       
                }
            }

            return Ok(ApiResponse<ProductDto>.SuccessResponse(data.Data!, data.Message, 200));
        }

    }
}