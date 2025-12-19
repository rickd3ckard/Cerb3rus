# Cerb3rus
Cerb3rus is a c# dotnet console app designed to compile a list of existing online domains that accept http requests, using the [crt.sh](https://crt.sh/) certificate database. The main feature beeing the ability to **filter domains by top-level domain**. The resulting list can either be stored as a .txt file locally or sent to a **sql database**.

## Installation and usage
Fetch the github repository and navigate to the cerb3rus project folder, then run the console app using dotnet :
```bash
git clone https://github.com/rickd3ckard/Cerb3rus.git
```
```bash
cd Cerb3rus -> cd Cerb3rus
```
```bash
dotnet run -- -f .be
```
The output is written inside a .txt file as json List<string> that lands in the project directory.

### Command Arguments
Here find  a list of the different possible arguments:
| Argument | Description | Example
|-----|------------|----------|
| `-f` | Filter for top-level domain | .be |
| `-o` | Custom output file name or full file path (overwrite default) | nicelist.txt |
| `-s` | Sql Server Address | srv1424.hstgr.io |
| `-u` | Sql UserName | u157358140_Cerberus |
| `-p` | Sql Password | NicePassword123! |
| `-d` | Sql Database Name | u157358140_Cerberus! |

### Command Examples
Simple default execution:
``` bash
dotnet run -- -f .be 
```
Changing the output file name: 
``` bash
dotnet run -- -f .be -o Nicelist.txt
```
Changing the output file name and location: 
``` bash
dotnet run -- -f .be -o C:/Users/Ricky/Desktop/Nicelist.txt
```
Sending everything to an sql database:
``` bash
dotnet run -- -f .be -s srv1424.hstgr.io -u u157358140_Cerberus -p NicePassword123 -d u157358140_Cerberus
```
### Sql Database set up
The SQL database must contain a table named **domains** with a single column named **domain** as **TINYTEXT**.
| Column | Type     | Description |
|------|----------|-------------|
| `domain` | TINYTEXT | Stores domain names (e.g. example.com) |

Here find the SQL command to create such table:

``` SQL
CREATE TABLE domains (
    domain TINYTEXT
);
```
