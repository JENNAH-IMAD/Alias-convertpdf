import * as XLSX from 'xlsx';

export function downloadExcel(
  filename: string,
  columns: string[],
  rows: (string | number | null | undefined)[][],
  decimalColumns: number[] = [],
) {
  const sheet = XLSX.utils.aoa_to_sheet([columns, ...rows]);
  for (const column of decimalColumns) {
    rows.forEach((row, index) => {
      if (typeof row[column] === 'number') {
        const cell = sheet[XLSX.utils.encode_cell({ r: index + 1, c: column })];
        if (cell) cell.z = '0.00';
      }
    });
  }
  sheet['!cols'] = columns.map((label, index) => ({
    wch: Math.min(
      64,
      Math.max(
        12,
        label.length + 2,
        ...rows.slice(0, 200).map((row) => String(row[index] ?? '').length + 2),
      ),
    ),
  }));
  if (columns.length) sheet['!autofilter'] = { ref: sheet['!ref']! };
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, sheet, 'Données');
  XLSX.writeFile(workbook, `${filename.replace(/\.(xlsx|csv|txt|tsv)$/i, '')}.xlsx`, {
    compression: true,
  });
}
