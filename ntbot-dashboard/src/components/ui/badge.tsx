import React from 'react';
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "../../lib/utils"

const badgeVariants = cva(
  "inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border",
  {
    variants: {
      variant: {
        default: "bg-blue-100 text-blue-800 border-blue-200",
        secondary: "bg-gray-100 text-gray-800 border-gray-200",
        destructive: "bg-red-100 text-red-800 border-red-200",
        outline: "text-slate-400 border-slate-600",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <span className={cn(badgeVariants({ variant }), className)} {...props} />
  )
}

export { Badge }
