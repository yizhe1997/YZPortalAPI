﻿using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using YZPortal.API.Infrastructure.Mediatr;
using YZPortal.Core.Domain.Contexts;
using YZPortal.Core.Domain.Database.Users;
using YZPortal.Core.Error;

namespace YZPortal.API.Controllers.Users.ResetPassword
{
    public static class Create
    {
        public class Request : IRequest<Model>
        {
            public string? Email { get; set; }
            public string CallbackUrl { get; set; } = "{0}";
        }

        public class Validator : AbstractValidator<Request>
        {
            public Validator()
            {
                RuleFor(c => c.Email).NotNull().NotEmpty().EmailAddress();
            }
        }

        public class Model
        {
            public string? CallbackUrl { get; set; }
        }

        internal class RequestHandler : BaseRequestHandler<Request, Model>
        {
            IWebHostEnvironment Environment { get; }

            public RequestHandler(PortalContext dbContext, IMapper mapper, IHttpContextAccessor httpContext, CurrentContext userAccessor, IWebHostEnvironment hostingEnvironment) : base(dbContext, mapper, httpContext, userAccessor)
            {
                Environment = hostingEnvironment;
            }

            public override async Task<Model> Handle(Request request, CancellationToken cancellationToken)
            {
                var user = await Database.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (user == null) throw new RestException(HttpStatusCode.NotFound, "User not found.");

                var passwordReset = new UserPasswordReset { Id = Guid.NewGuid(), Email = request.Email, User = user, ValidUntilDateTime = DateTime.UtcNow + TimeSpan.FromDays(3), CallbackUrl = request.CallbackUrl };
                passwordReset.CallbackUrl = string.Format(passwordReset.CallbackUrl, passwordReset.Token);

                Database.UserPasswordResets.Add(passwordReset);
                await Database.SaveChangesAsync();

                // Return callback url only in dev mode due to security risk
                if (Environment.IsDevelopment())
                {
                    return Mapper.Map<Model>(passwordReset);
                }

                return new Model() { CallbackUrl = string.Empty };
            }
        }
    }
}