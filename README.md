## Usages:
CsvColumnSelector.exe -i "C:\Dev\csv-file.csv" -c "full_address:address" "shape" -o "C:\Dev\subset-csv-file.csv" 

CsvFileSplitter.exe -i "C:\Dev\csv-file.csv" -g "Month Year:.*?(\d{4})" -o "C:\Dev\\{group}.csv"
