using AutoMapper;
using System;
using System.Collections.Generic;
using Blog_Assignment;
using BankAppAPI.Models;
using BankAppAPI.Dtos;

namespace Blog_Assignment
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        { 

            // Account <-> AccountDto
            CreateMap<Account, AccountDto>().ReverseMap();

            // Customer <-> CustomerDto
            CreateMap<Customer, CustomerDto>().ReverseMap(); 

            // Transaction <-> TransactionDto
            CreateMap<Transaction, TransactionDto>().ReverseMap(); 
            
            // Loan <-> LoanDto
            CreateMap<Loan, LoanDto>().ReverseMap(); 
            
            // Card <-> CardDto
            CreateMap<Card, CardDto>().ReverseMap(); 
            
            // Disposition <-> DispositionDto
            CreateMap<Disposition, DispositionDto>().ReverseMap(); 
            
            // AccountType <-> AccountTypeDto
            CreateMap<AccountType, AccountTypeDto>().ReverseMap(); 
        }

    }
}
