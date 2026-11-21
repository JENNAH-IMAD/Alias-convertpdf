'use client';

import { FileSpreadsheet } from 'lucide-react';
import { downloadExcel } from '@/lib/download-excel';
import { Button } from './ui/button';

export function TemplateExcelButton({ name, columns }: { name: string; columns: string[] }) {
  return (
    <Button
      permission="documents.export"
      variant="outline"
      disabled={!columns.length}
      title="Télécharger la structure Excel"
      onClick={() => downloadExcel(name, columns, [])}
    >
      <FileSpreadsheet size={15} /> Excel
    </Button>
  );
}
