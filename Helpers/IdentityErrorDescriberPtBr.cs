using Microsoft.AspNetCore.Identity;

namespace PecasAutomotivas.Helpers;

public sealed class IdentityErrorDescriberPtBr : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => E(nameof(DefaultError), "Ocorreu um erro inesperado.");
    public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Os dados foram alterados por outra operação. Tente novamente.");
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "Senha incorreta.");
    public override IdentityError InvalidToken() => E(nameof(InvalidToken), "Token inválido.");
    public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Já existe um usuário com este login.");
    public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), "Nome de usuário inválido.");
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), "E-mail inválido.");
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), "Este e-mail já está cadastrado.");
    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), "Este e-mail já está cadastrado.");
    public override IdentityError InvalidRoleName(string? role) => E(nameof(InvalidRoleName), "Perfil inválido.");
    public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), "Perfil já existente.");
    public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "Usuário já possui senha.");
    public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "Bloqueio não habilitado para este usuário.");
    public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), "Usuário já possui este perfil.");
    public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), "Usuário não possui este perfil.");
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"A senha precisa ter pelo menos {length} caracteres.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), $"A senha precisa ter pelo menos {uniqueChars} caracteres diferentes.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "A senha precisa ter pelo menos um símbolo (ex.: @, #, !).");
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "A senha precisa ter pelo menos um número.");
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "A senha precisa ter pelo menos uma letra minúscula.");
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "A senha precisa ter pelo menos uma letra maiúscula.");

    private static IdentityError E(string code, string description) => new() { Code = code, Description = description };
}
