using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ostagar.Modules.CashBook;

namespace Ostagar.Api.Controllers;

public sealed class BankingExceptionFilter : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is BankingRuleException rule)
            context.Result = new BadRequestObjectResult(new { reason = rule.Code.ToString() });
        else if (context.Exception is ArgumentException)
            context.Result = new BadRequestObjectResult(new { reason = BankingErrorCode.InvalidAccount.ToString() });
        else if (context.Exception is DbUpdateException { InnerException: PostgresException postgres }
                 && postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation
                     or PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RaiseException)
            context.Result = new ConflictObjectResult(new { reason = BankingErrorCode.DatabaseRefused.ToString() });
        context.ExceptionHandled = context.Result is not null;
    }
}
