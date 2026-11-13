using PractitionerService.Domain.Common.Exceptions;
using System.Net.Mail;

namespace PractitionerService.Domain.Practitioners;

/// <summary>
/// Aggregate root. Flat model matching the task spec 3.1.
/// Knows nothing about FHIR — mapping lives on the API boundary.
/// </summary>
public sealed class Practitioner
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string Speciality { get; private set; } = null!;
    public string PlaceOfWork { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }          // optimistic concurrency
    public bool IsDeleted { get; private set; }       // soft delete
    public string? SourceEventId { get; private set; } // import idempotency

    private Practitioner() { }

    public static Practitioner Create(string firstName, string lastName, string phone, string speciality, string placeOfWork, string email, bool isActive, DateTime now, string? sourceEventId = null)
    {
        Validate(firstName, lastName, phone, speciality, placeOfWork, email);

        return new Practitioner
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Phone = NormalizePhone(phone),
            Speciality = speciality.Trim(),
            PlaceOfWork = placeOfWork.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            IsDeleted = false,
            SourceEventId = string.IsNullOrWhiteSpace(sourceEventId) ? null : sourceEventId
        };
    }

    public void Update(string firstName, string lastName, string phone, string speciality, string placeOfWork, string email, bool isActive, DateTime now)
    {
        EnsureNotDeleted();
        Validate(firstName, lastName, phone, speciality, placeOfWork, email);

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Phone = NormalizePhone(phone);
        Speciality = speciality.Trim();
        PlaceOfWork = placeOfWork.Trim();
        Email = email.Trim().ToLowerInvariant();
        IsActive = isActive;
        UpdatedAt = now;
        Version++;
    }

    public void MarkAsDeleted(DateTime now)
    {
        if (IsDeleted) return;

        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
        Version++;
    }

    private static void Validate(string firstName, string lastName, string phone, string speciality, string placeOfWork, string email)
    {
        RequireNonEmpty(firstName, nameof(firstName), maxLen: 100);
        RequireNonEmpty(lastName, nameof(lastName), maxLen: 100);
        RequireNonEmpty(speciality, nameof(speciality), maxLen: 200);
        RequireNonEmpty(placeOfWork, nameof(placeOfWork), maxLen: 200);
        RequireNonEmpty(email, nameof(email), maxLen: 254);

        if (!LooksLikeEmail(email))
            throw new DomainException($"Email '{email}' имеет некорректный формат.");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException($"Врач {Id} удалён и не может быть изменён.");
    }

    private static void RequireNonEmpty(string value, string field, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"Поле '{field}' обязательно.");
        if (value.Length > maxLen)
            throw new DomainException($"Поле '{field}' длиннее {maxLen} символов.");
    }

    private static bool LooksLikeEmail(string value)
    {
        return MailAddress.TryCreate(value, out var addr) && addr.Address == value;
    }

    private static string NormalizePhone(string phone)
    {
        var trimmed = phone.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        if (string.IsNullOrEmpty(digits))
            throw new DomainException($"Поле 'phone' не содержит цифр.");

        return hasPlus ? "+" + digits : digits;
    }
}