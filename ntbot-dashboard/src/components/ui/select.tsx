import React from 'react';

interface SelectProps {
  value: string;
  onValueChange: (value: string) => void;
  children: React.ReactNode;
}

export function Select({ value, onValueChange, children }: SelectProps) {
  const [isOpen, setIsOpen] = React.useState(false);

  const handleSelect = (newValue: string) => {
    onValueChange(newValue);
    setIsOpen(false);
  };

  return (
    <div className="relative inline-block">
      {React.Children.map(children, (child) => {
        if (React.isValidElement(child)) {
          if (child.type === SelectTrigger) {
            return React.cloneElement(child as React.ReactElement<any>, {
              onClick: () => setIsOpen(!isOpen),
              value,
            });
          }
          if (child.type === SelectContent && isOpen) {
            return React.cloneElement(child as React.ReactElement<any>, {
              onSelect: handleSelect,
            });
          }
        }
        return null;
      })}
    </div>
  );
}

interface SelectTriggerProps {
  children: React.ReactNode;
  className?: string;
  onClick?: () => void;
  value?: string;
}

export function SelectTrigger({ children, className = '', onClick }: SelectTriggerProps) {
  return (
    <button
      onClick={onClick}
      className={`px-3 py-2 border border-gray-600 rounded-md bg-gray-800 text-white text-left ${className}`}
    >
      {children}
    </button>
  );
}

export function SelectValue({ placeholder, value }: { placeholder?: string; value?: string }) {
  return <span>{value || placeholder}</span>;
}

interface SelectContentProps {
  children: React.ReactNode;
  onSelect?: (value: string) => void;
}

export function SelectContent({ children, onSelect }: SelectContentProps) {
  return (
    <div className="absolute mt-2 w-full bg-gray-800 border border-gray-600 rounded-md shadow-lg z-10">
      {React.Children.map(children, (child) => {
        if (React.isValidElement(child) && onSelect) {
          return React.cloneElement(child as React.ReactElement<any>, { onSelect });
        }
        return child;
      })}
    </div>
  );
}

interface SelectItemProps {
  value: string;
  children: React.ReactNode;
  onSelect?: (value: string) => void;
}

export function SelectItem({ value, children, onSelect }: SelectItemProps) {
  return (
    <div
      className="px-3 py-2 hover:bg-gray-700 cursor-pointer text-white"
      onClick={() => onSelect?.(value)}
    >
      {children}
    </div>
  );
}
