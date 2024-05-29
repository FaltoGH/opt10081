# opt10081

## CSV file path format
/data/(year)/(month)/(day)

Each element has no leading zero.

## CSV content row format
종목코드,현재가,거래량,거래대금,시가,고가,저가,수정주가구분,수정비율

- Each element has no leading zero.
- CSV Format follows [RFC 4180](https://www.rfc-editor.org/rfc/rfc4180).
- 종목코드, 수정주가구분은 큰 따옴표로 감싸져 있다.
