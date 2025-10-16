window.printTable = function (tableSelector, titulo, opciones = {}) {
    console.log('Iniciando impresión de tabla:', tableSelector);

    // Configuración por defecto
    const config = {
        orientacion: opciones.orientacion || 'landscape', // 'landscape' o 'portrait'
        tamanoFuente: opciones.tamanoFuente || '9px',
        mostrarFecha: opciones.mostrarFecha !== false,
        colorEncabezado: opciones.colorEncabezado || '#03346c',
        excluirColumnas: opciones.excluirColumnas || ['Acciones'], // Columnas a excluir
        ...opciones
    };

    // Buscar la tabla
    const tableElement = document.querySelector(tableSelector || '.mud-table');

    if (!tableElement) {
        alert('No se encontró la tabla para imprimir');
        console.error('Tabla no encontrada:', tableSelector);
        return;
    }

    // Obtener encabezados
    const headers = [];
    const headerCells = tableElement.querySelectorAll('thead th');

    headerCells.forEach(th => {
        const headerText = th.textContent.trim();
        if (headerText && !config.excluirColumnas.includes(headerText)) {
            headers.push(headerText);
        }
    });

    if (headers.length === 0) {
        alert('No se encontraron encabezados en la tabla');
        return;
    }

    console.log('Encabezados encontrados:', headers);

    // Obtener filas de datos
    const rows = [];
    const tableRows = tableElement.querySelectorAll('tbody tr');

    tableRows.forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length === 0) return;

        const rowData = [];
        let cellIndex = 0;

        cells.forEach((cell, index) => {
            // Obtener el data-label para saber qué columna es
            const dataLabel = cell.getAttribute('data-label');

            // Verificar si esta columna debe ser excluida
            if (dataLabel && config.excluirColumnas.includes(dataLabel)) {
                return; // Saltar esta celda
            }

            // Si es la última celda y no tiene data-label, probablemente sea "Acciones"
            if (index === cells.length - 1 && !dataLabel) {
                return; // Saltar última columna (Acciones)
            }

            // Extraer contenido de la celda
            let content = '';

            // Buscar chips de MudBlazor
            const chip = cell.querySelector('.mud-chip');
            if (chip) {
                content = chip.textContent.trim();
            }
            // Buscar texto en divs con clase específica
            else if (cell.querySelector('.font-weight-medium')) {
                content = cell.querySelector('.font-weight-medium').textContent.trim();
            }
            // Buscar en mud-typography
            else if (cell.querySelector('.mud-typography')) {
                const typography = cell.querySelectorAll('.mud-typography');
                const texts = Array.from(typography).map(t => t.textContent.trim()).filter(t => t);
                content = texts.join(' ');
            }
            // Buscar fechas en formato específico
            else if (cell.querySelector('.d-flex.flex-column')) {
                const dateDiv = cell.querySelector('.d-flex.flex-column');
                const allText = Array.from(dateDiv.querySelectorAll('.mud-typography'))
                    .map(t => t.textContent.trim())
                    .filter(t => t)
                    .join(' ');
                content = allText;
            }
            // Contenido general
            else {
                content = cell.textContent.trim();
            }

            // Limpiar contenido
            content = content
                .replace(/\s+/g, ' ')
                .replace('Sin email', '-')
                .replace('Sin DPI', '-')
                .replace('Sin rol', '-')
                .replace('Sin sucursal', '-')
                .replace('Sin teléfono', '-')
                .replace('Sin fecha', '-')
                .trim();

            if (cellIndex < headers.length) {
                rowData.push(content || '-');
                cellIndex++;
            }
        });

        if (rowData.length > 0) {
            rows.push(rowData);
        }
    });

    console.log('Filas procesadas:', rows.length);

    if (rows.length === 0) {
        alert('No hay datos para imprimir');
        return;
    }

    // Construir HTML de la tabla
    let tableHTML = `
        <div class="print-container">
            <div class="print-header">
                <h1>${titulo || 'Reporte'}</h1>
                ${config.mostrarFecha ? `
                <p>Fecha: ${new Date().toLocaleDateString('es-GT', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    })}</p>
                ` : ''}
                <p>Total de registros: ${rows.length}</p>
            </div>
            <table class="printable-table">
                <thead>
                    <tr>
                        ${headers.map(h => `<th>${h}</th>`).join('')}
                    </tr>
                </thead>
                <tbody>
                    ${rows.map(row => `
                        <tr>
                            ${row.map(cell => `<td>${cell}</td>`).join('')}
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;

    // Estilos de impresión
    const printStyles = `
        @media print {
            body * { 
                visibility: hidden; 
            }
            
            .print-container, 
            .print-container * { 
                visibility: visible; 
            }
            
            .print-container {
                position: absolute;
                left: 0;
                top: 0;
                width: 100%;
                padding: 15px;
            }
            
            .print-header {
                text-align: center;
                margin-bottom: 20px;
                border-bottom: 3px solid ${config.colorEncabezado};
                padding-bottom: 15px;
            }
            
            .print-header h1 {
                color: ${config.colorEncabezado};
                font-size: 28px;
                margin: 0 0 5px 0;
                font-weight: bold;
            }
            
            .print-header p {
                color: #666;
                font-size: 12px;
                margin: 5px 0;
            }
            
            .printable-table {
                width: 100%;
                border-collapse: collapse;
                font-size: ${config.tamanoFuente};
                margin-top: 10px;
            }
            
            .printable-table th,
            .printable-table td {
                border: 1px solid #333;
                padding: 5px 6px;
                text-align: left;
                word-wrap: break-word;
            }
            
            .printable-table th {
                background-color: ${config.colorEncabezado} !important;
                color: white !important;
                font-weight: bold;
                font-size: calc(${config.tamanoFuente} + 1px);
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            
            .printable-table tbody tr:nth-child(even) {
                background-color: #f5f5f5 !important;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            
            @page {
                size: ${config.orientacion};
                margin: 12mm;
            }
        }

        @media screen {
            .print-container {
                display: none;
            }
        }
    `;

    // Insertar elementos en el DOM
    const printContainer = document.createElement('div');
    printContainer.innerHTML = tableHTML;
    document.body.appendChild(printContainer);

    const styleElement = document.createElement('style');
    styleElement.textContent = printStyles;
    document.head.appendChild(styleElement);

    console.log('Ejecutando impresión...');

    // Ejecutar impresión con delay
    setTimeout(() => {
        window.print();
    }, 250);

    // Limpieza
    const cleanup = () => {
        console.log('Limpiando elementos...');
        if (printContainer && printContainer.parentNode) {
            document.body.removeChild(printContainer);
        }
        if (styleElement && styleElement.parentNode) {
            document.head.removeChild(styleElement);
        }
        window.onafterprint = null;
    };

    window.onafterprint = cleanup;
    setTimeout(cleanup, 30000);
};