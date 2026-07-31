Act as a Senior Software Architect. Goal is to build a .Net/Blazor standalone application designed to generate secure passwords.

Please provide plan for software developer of the following:
1.create plan of create from scratch .Net/Blazor standalone application with single page
2. Single page should contain:

hash algorithm dropdown
password length dropdown from 4 to 32 with default 16
symbol types grouped checkboxes:
lowercase characters
digits
upper case characters
symbols
all options should have short example in braces
by default checked all except symbols
master password input - omly alphanumeric allowed
save master password checkbox (unchecked by default)
site url input
site host name label
password readonly input
button which copy password into clipboard (when password copied, green checkbox icon should appear)
When url input filled, it should check url is correct and extract host part into site host name label

When master password filled, site host name label calculated, password should be genarated with choosen hash algorithm

when password generated depends on state of save master password checkbox master password can be saved in local storage in encrypted form or cleared up if it waspresent before

password generation:

hostname shoul be hashed with master password as salt
hash shoul be converted into password depends on settings of password length, symbol types into human-readable string password
on second application load if master password available in local storage in encrypted form it should be decrypted and placed into master password input and save master password checkbox checked

create plan for unit tests

create plan for end-to-end ui automated tests

plans should be saved into file plan-{issue title}-develop.md, plan-{issue title}-unittests.md plan-{issue title}-uitests.md and commited