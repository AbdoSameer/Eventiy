using Domain.Common;

namespace Domain.Errors
{
    public class UserErrors
    {
        public static Error UserNotAuthenticated()
            => Error.Unauthorized(
               "UserNotAuthenticated",
               "The user is not authenticated.");

        public static Error EmailEmpty()
            => Error.Validation(
                "EmailEmpty",
                "The email address cannot be empty.");

        public static Error EmailInvalid()
            => Error.Validation(
                "EmailInvalid",
                "The email address is not valid.");

        public static Error EmailTooLong()
            => Error.Validation(
                "EmailTooLong",
                "The email address is too long.");

        public static Error RoleInvalid()
            => Error.Validation(
                "RoleInvalid",
                "The role is not valid.");
        
        public static  Error EmailAlreadyExists ()
            => Error.Conflict("User.EmailAlreadyExists", "A user with this email already exists.");

        public static  Error InvalidCredentials ()
            => Error.Validation("User.InvalidCredentials", "Email or password is incorrect.");

        public static Error PendingApproval()
            => Error.Unauthorized(
                "User.PendingApproval",
                "Organizer registration is pending admin approval.");

        public static Error NotOrganizer()
            => Error.Validation(
                "User.NotOrganizer",
                "Only organizer accounts can be approved.");

        public static  Error NotFound ()
            => Error.NotFound("User.NotFound", "User was not found.");

        public static Error RefreshTokenNotFoundOrInactive()
            => Error.Unauthorized(
                "User.RefreshTokenNotFoundOrInactive",
                "The refresh token was not found or is no longer active.");

        public static Error RefreshTokenExpired()
            => Error.Unauthorized(
                "User.RefreshTokenExpired",
                "The refresh token has expired. Please log in again.");

        public static Error RefreshTokenReused()
            => Error.Unauthorized(
                "User.RefreshTokenReused",
                "Refresh token reuse detected. All active sessions have been revoked for security.");
    }
}