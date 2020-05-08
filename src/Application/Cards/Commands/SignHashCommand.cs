﻿using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using EGID.Application.Common.Exceptions;
using EGID.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace EGID.Application.Cards.Commands
{
    public class SignHashCommand : IRequest<string>
    {
        [Required] public string Pin2 { get; set; }

        [Required] public string Base64Sha512DataHash { get; set; }

        #region Validator

        public class SignHashValidator : AbstractValidator<SignHashCommand>
        {
            public SignHashValidator()
            {
                RuleFor(s => s.Pin2).NotEmpty().WithMessage("من فضلك لتوقيع الملفات ادخل رمز PIN2 الخاص بك.");

                RuleFor(s => s.Base64Sha512DataHash)
                    .NotEmpty().WithMessage("من فضلك اضف بصمة الملفات من نوع base64 sha512 للتوقيع.")
                    .MaximumLength(512).WithMessage("خطأ: هذه البيانات غير مطابقة للشروط.");
            }
        }

        #endregion

        #region Handler

        public class SignHashCommandHandler : IRequestHandler<SignHashCommand, string>
        {
            private readonly IDigitalSignatureService _digitalSignatureService;
            private readonly IEgidDbContext _context;
            private readonly ICurrentUserService _currentUser;
            private readonly ICardManagerService _cardManager;

            public SignHashCommandHandler(IDigitalSignatureService digitalSignatureService, IEgidDbContext context,
                ICurrentUserService currentUser, ICardManagerService cardManager)
            {
                _digitalSignatureService = digitalSignatureService;
                _context = context;
                _currentUser = currentUser;
                _cardManager = cardManager;
            }

            public async Task<string> Handle(SignHashCommand request, CancellationToken cancellationToken)
            {
                var card = await _context.Cards.FindAsync(_currentUser.UserId);

                if (card is null) throw new EntityNotFoundException("Card", _currentUser.UserId);

                if (!_cardManager.VerifyPin2(card, request.Pin2))
                    throw new BadRequestException(new[] {"خطأ رمز PIN2 غير صحيح."});

                var citizen = await _context.CitizenDetails.FindAsync(card.CitizenId);

                if (citizen is null) throw new EntityNotFoundException("Citizen", card.CitizenId);

                var signature = _digitalSignatureService.SignHash(request.Base64Sha512DataHash, citizen.PrivateKey);

                return _currentUser.UserId + signature;
            }
        }

        #endregion
    }
}