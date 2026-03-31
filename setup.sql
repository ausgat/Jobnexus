create database JobNexus;
use JobNexus;

create table Profile(
username varchar(50) primary key,
password varchar(50),
email varchar(100),
name varchar(50),
bio varchar(200),
location varchar(200)
);

create table Certification(
cert_id int primary key,
cert_name varchar(50),
date_given datetime,
username varchar(50) null,
foreign key (username) references
Profile(username)
);

create table Skills(
skill_id int primary key,
skill_name varchar(50),
category varchar(100)
);

create table JobSource(
source_id int primary key,
source_name varchar(150)
);

create table Company(
company_id int primary key,
company_name varchar(100),
Website_url varchar(250),
Industry varchar(100)
);

create table Job(
Job_id int primary key,
title varchar(50),
description varchar(100),
apply_url varchar(50),
pay int,
date_posted datetime,
source_id int null,
company_id int null,
foreign key (source_id) references
JobSource(source_id),
foreign key (company_id) references
Company(company_id)
);

create table Requires (
skill_id int,
job_id int,
primary key (skill_id, job_id),
foreign key (skill_id) references Skills(skill_id),
foreign key (job_id) references Job(job_id)
);

create table Applied (
username varchar(50),
job_id int,
primary key (username, job_id),
foreign key (username) references Profile(username),
foreign key (job_id) references Job(job_id)
);

create table Obtained (
skill_id int,
username varchar(50),
primary key (skill_id, username),
foreign key (skill_id) references Skills(skill_id),
foreign key (username) references Profile(username)
);

create table Resume (
username varchar(50) primary key,
Job_Exp varchar(300),
education varchar(300),
recommendations varchar(300),
projects varchar(300),
foreign key (username) references Profile(username)
);
